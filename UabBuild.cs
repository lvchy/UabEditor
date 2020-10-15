using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System;

public class UabBuild
{

    private static List<UabInfoItem> buildOnePipeline(List<AssetBundleBuild> abbuilds, BuildAssetBundleOptions op) {
        string targetroot = UabFileUtil.PathCombine(UabEditorDef.UabRoot, UabDef.UAB_BUNDLE_ROOT);
        UabFileUtil.EnsureDir(targetroot);

        AssetBundleManifest abm = BuildPipeline.BuildAssetBundles(targetroot, abbuilds.ToArray(), op, EditorUserBuildSettings.activeBuildTarget);
        if (abm == null) {
            Debug.LogError("uab build fail");
            return null;
        }

        return createUabInfo(abm);
    }

    public static HashSet<string> allNotLoadedRes = new HashSet<string>();
    public static HashSet<string> neededRes = new HashSet<string>();
    public static HashSet<string> missPackRes = new HashSet<string>();

    public static HashSet<string> beginAssetBundleNames = new HashSet<string>();
    public static HashSet<string> collectAssetBundleNames = new HashSet<string>();
    public static void Build(UabConfig uabcfg) {
        beginAssetBundleNames.Clear();
        var abnames = AssetDatabase.GetAllAssetBundleNames();
        for (int i = 0;i<abnames.Length;i++)
        {
            beginAssetBundleNames.Add(abnames[i]);
        }
        
        string targetroot = UabFileUtil.PathCombine(UabEditorDef.UabRoot, UabDef.UAB_BUNDLE_ROOT);
        UabFileUtil.EnsureDir(targetroot);

        for (int i = 0;i<uabcfg.G.Length;i++)
        {
            if (!uabcfg.G[i].enabled)
            {
                continue;
            }
            if (!uabcfg.G[i].isasset)
            {
                continue;
            }


            string[] files = UabCollect.collectFiles(uabcfg.G[i]);
            for (int j = 0; j < files.Length; j++)
            {
                if (!uabcfg.G[i].resourceasset)
                {
                    //Debug.LogWarning("file :" + files[j] + " check depend :");
                    var d = AssetDatabase.GetDependencies(files[j],true);
                    for (int m = 0; m < d.Length; m++)
                    {
                        //Debug.LogError("dependency is " + d[m]);
                        if (!neededRes.Contains(d[m]))
                            neededRes.Add(d[m]);
                    }
                }
                else
                {
                    if (!allNotLoadedRes.Contains(files[j]))
                        allNotLoadedRes.Add(files[j]);
                }
            }

        }


        //foreach(var res in allNotLoadedRes)
        //{
        //    if (!neededRes.Contains(res))
        //        Debug.LogError("检测到无用资源 :" + res);
        //}


        //return;

        List<UabInfoItem> uabItems = new List<UabInfoItem>();

        List<AssetBundleBuild> abbuilds = new List<AssetBundleBuild>();
        List<UabInfoItem> items = null;
        for (int i = 0; i < uabcfg.G.Length; i++) {
            if (!uabcfg.G[i].enabled) {
                continue;
            }
            if (!uabcfg.G[i].isasset) {
                continue;
            }
            List<AssetBundleBuild> l = UabCollect.CollectABBuilds(uabcfg.G[i]);
            if (uabcfg.G[i].name == "video") {
                items = buildOnePipeline(l, BuildAssetBundleOptions.UncompressedAssetBundle | BuildAssetBundleOptions.DeterministicAssetBundle);
                if (items == null) {
                    return;
                }
                uabItems.AddRange(items);
            } else {
                abbuilds.AddRange(l);
            }
        }


        List<string> remove = new List<string>();
        foreach (var cbuild in beginAssetBundleNames)
        {
            if (!collectAssetBundleNames.Contains(cbuild))
            {
                remove.Add(cbuild);
            }
        }
        foreach (var rem in remove)
        {
            AssetDatabase.RemoveAssetBundleName(rem, true);
        }



        BuildAssetBundleOptions op = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DeterministicAssetBundle;
        items = buildOnePipeline(abbuilds, op);
        if (items == null) {
            return;
        }
        uabItems.AddRange(items);
        
        saveUabConfig(uabcfg);
        saveUabInfo(uabItems);

        AssetDatabase.Refresh();

        Debug.Log("uab build success...");
    }

    public static void Build(string cfgpath) {
        UabConfig cfg = UabCommon.LoadUabConfigFromFile(cfgpath);
        if (cfg == null || cfg.G == null) {
            Debug.LogError("uab cfg error! ");
            return;
        }
        Build(cfg);
    }

    static void saveUabConfig(UabConfig uabcfg) {
        string path = UabFileUtil.PathCombine(UabEditorDef.UabRoot, UabDef.UAB_CONFIG_NAME);
        string json = JsonUtility.ToJson(uabcfg, true);
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(json));
    }

    static List<UabInfoItem> createUabInfo(AssetBundleManifest abm) {
        List<UabInfoItem> items = new List<UabInfoItem>();

        string[] allbundles = abm.GetAllAssetBundles();
        foreach (string bundle in allbundles) {
            string bundlename = Path.GetFileName(bundle);
            string bundlepath = UabFileUtil.PathCombine(UabEditorDef.UabRoot, UabDef.UAB_BUNDLE_ROOT, bundle);

            UabInfoItem item = new UabInfoItem();
            item.N = bundlename;
            item.S = UabFileUtil.GetFileSize(bundlepath);
            item.H = UabFileUtil.CalcFileMD5(bundlepath);
            item.R = false;

            string[] d = abm.GetDirectDependencies(bundle);
            item.D = new string[d.Length];
            for (int i = 0; i < d.Length; i++) {
                item.D[i] = Path.GetFileNameWithoutExtension(d[i]);
            }
            items.Add(item);
        }

        checkLoopDepend(items);

        return items;
    }

    static void saveUabInfo(List<UabInfoItem> items) {
        UabInfo info = new UabInfo();
        info.G = items.ToArray();
        string json = JsonUtility.ToJson(info, true);
        string infopath = UabFileUtil.PathCombine(UabEditorDef.UabRoot, UabDef.ASSET_INFO_NAME);
        File.WriteAllBytes(infopath, Encoding.UTF8.GetBytes(json));
    }

    static void checkLoopDepend(List<UabInfoItem> items) {
        Dictionary<string, UabInfoItem> dict = new Dictionary<string, UabInfoItem>();
        foreach (UabInfoItem item in items) {
            dict.Add(Path.GetFileNameWithoutExtension(item.N), item);
        }
        HashSet<string> checkedItem = new HashSet<string>();
        foreach (UabInfoItem item in items) {
            _checkLoopDepend(item, dict, checkedItem);
        }
    }
    static void _checkLoopDepend(UabInfoItem item, Dictionary<string, UabInfoItem> dict, HashSet<string> checkedItem, List<string> hspath = null) {
        string n = Path.GetFileNameWithoutExtension(item.N);
        if (checkedItem.Contains(n) || item.D.Length == 0) {
            if (!checkedItem.Contains(n)) {
                checkedItem.Add(n);
            }
            return;
        }
        if (hspath == null) {
            hspath = new List<string>();
        }
        hspath.Add(n);
        bool loopdepend = false;       
        foreach (string d in item.D) {
            if (hspath.Contains(d)) {
                string str = "";
                for (int i = 0; i < hspath.Count; i++) {
                    str += $"{hspath[i]} -> ";
                }
                str = str.Substring(0, str.Length - 4);
                str = str + " contain: " + d;
                //throw new Exception($"发现循环依赖 [ {str} ]");
                Debug.LogError($"发现循环依赖 [ {str} ]");
                loopdepend = true;
                break;
            }
            if (loopdepend)
                return;
            _checkLoopDepend(dict[d], dict, checkedItem, hspath);
        }
        hspath.Remove(n);
        if (!checkedItem.Contains(n)) {
            checkedItem.Add(n);
        }
    }
}
