using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class UabCollect {

    public static string[] collectDirPath(string sourcePath,int layer)
    {
        if (!Directory.Exists(sourcePath))
        {
            return null;
        }

        if (layer == 0)
            return new string[1] {sourcePath};

        DirectoryInfo dirInfo = new DirectoryInfo(sourcePath);

        DirectoryInfo[] dirInfos = null;
        string[] dirNames = null;

        while (layer > 0)
        {
            if (dirInfos == null)
            {
                dirInfos = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
            }
            else
            {
                List<DirectoryInfo> newInfos = new List<DirectoryInfo>();
                for (int i = 0;i<dirInfos.Length;i++)
                {
                    var smInfo = dirInfos[i].GetDirectories("*",SearchOption.TopDirectoryOnly);
                    if (smInfo != null)
                    {
                        for (int j = 0;j<smInfo.Length;j++)
                        {
                            newInfos.Add(smInfo[j]);
                        }
                    }
                }

                dirInfos = newInfos.ToArray();
            }

            layer--;
        }

        if (dirInfos == null)
            return null;
        
        dirNames = new string[dirInfos.Length];        
        for (int i = 0;i<dirInfos.Length;i++)
        {
            dirNames[i] = dirInfos[i].FullName.Replace("\\","/").Replace(Application.dataPath, "Assets");        
        }
        return dirNames;
    }
    public static string[] collectFiles(UabConfigGroup group) {
 
        List<string> allfiles = new List<string>();
        string[] arrpath = group.realassetpath.Split(',');
        foreach (var realpath in arrpath) {
            if (!Directory.Exists(realpath)) {
                continue;
            }            
            SearchOption op = group.onlytop ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;

            //var files = Directory.GetFiles(realpath, "*.*", op).ToList();
            //files = (from f in files
            //         where
            //         (File.GetAttributes(f) & FileAttributes.Hidden) != FileAttributes.Hidden &&
            //         Path.GetExtension(f) != ".meta"
            //         select f.Replace('\\', '/')).ToList();
            //if (!string.IsNullOrEmpty(group.extensions))
            //{
            //    string[] splits = group.extensions.Split(',');
            //    files = files.Where(f => splits.Contains(Path.GetExtension(f))).ToList();
            //}
            //allfiles.AddRange(files);


            string[] wantpath = collectDirPath(realpath, group.dirsearchlayer);

            if (wantpath == null) continue;

            for (int i = 0; i < wantpath.Length; i++)
            {
                var files = Directory.GetFiles(wantpath[i], "*.*", op).ToList();
                files = (from f in files
                         where
                         (File.GetAttributes(f) & FileAttributes.Hidden) != FileAttributes.Hidden &&
                         Path.GetExtension(f) != ".meta" && Path.GetExtension(f) != ".cs" && !f.Contains("@")
                         select f.Replace('\\', '/')).ToList();
                if (!string.IsNullOrEmpty(group.extensions))
                {
                    if (group.extensions != ".*")
                    {
                        string[] splits = group.extensions.Split(',');
                        files = files.Where(f => splits.Contains(Path.GetExtension(f))).ToList();
                    }
                }
                allfiles.AddRange(files);
            }
        }

        return allfiles.ToArray();
    }

    public static List<AssetBundleBuild> CollectABBuilds(UabConfigGroup cfggrp) {
        if (cfggrp.uabcount > 0) {
            return collectABBuilds_Group(cfggrp);
        } else {
            return collectABBuilds_Single(cfggrp);
        }
    }

    private static List<AssetBundleBuild> collectABBuilds_Single(UabConfigGroup cfggrp) {
        List<AssetBundleBuild> abbuilds = new List<AssetBundleBuild>();
        string[] files = collectFiles(cfggrp);
        for (int i = 0; i < files.Length; i++) {
            string filename = Path.GetFileNameWithoutExtension(files[i]);
            AssetBundleBuild abbuild = new AssetBundleBuild();
            abbuild.assetBundleName = string.Format("{0}_{1}", cfggrp.name, filename);//, UabDef.UAB_EXT);
            abbuild.assetNames = new string[] { files[i] };


            //if (AssetImporter.GetAtPath(files[i]).assetBundleName != abbuild.assetBundleName)
            //    AssetImporter.GetAtPath(files[i]).assetBundleName = abbuild.assetBundleName;            
            //if (!UabBuild.collectAssetBundleNames.Contains(abbuild.assetBundleName))
            //    UabBuild.collectAssetBundleNames.Add(abbuild.assetBundleName);

            abbuilds.Add(abbuild);
        }

        return abbuilds;
    }

    private static List<AssetBundleBuild> collectABBuilds_Group(UabConfigGroup cfggrp) {
        Dictionary<int, List<string>> map = new Dictionary<int, List<string>>();  
        string[] files = collectFiles(cfggrp);        
        for (int i = 0; i < files.Length; i++)
        {
            if(cfggrp.resourceasset)
                if (!UabBuild.neededRes.Contains(files[i]))
                    continue;

            string loadpath = getAssetLoadPath(files[i], cfggrp.loadpath);
            int hash = UabHash.BkdrHash(loadpath.ToLower());
            int abidx = hash % cfggrp.uabcount;
            List<string> l;
            if (!map.TryGetValue(abidx, out l))
            {
                l = new List<string>();
                map[abidx] = l;
            }
            l.Add(files[i]);
        }

        List<AssetBundleBuild> abbuilds = new List<AssetBundleBuild>();
        foreach (var kvp in map) {
            AssetBundleBuild abbuild = new AssetBundleBuild();
            abbuild.assetBundleName = getSplitBundleName(cfggrp.name, kvp.Key);
            abbuild.assetNames = kvp.Value.ToArray();
            for (int i = 0;i<abbuild.assetNames.Length;i++)
            {
                //if(AssetImporter.GetAtPath(abbuild.assetNames[i]).assetBundleName != abbuild.assetBundleName)
                //    AssetImporter.GetAtPath(abbuild.assetNames[i]).assetBundleName = abbuild.assetBundleName;
                //if(!UabBuild.collectAssetBundleNames.Contains(abbuild.assetBundleName))
                //    UabBuild.collectAssetBundleNames.Add(abbuild.assetBundleName);
            }
            abbuilds.Add(abbuild);
        }

        return abbuilds;
    }

    private static string getSplitBundleName(string grpname, int idx) {
        return string.Format("{0}_{1}", grpname.ToString(), idx);//, UabDef.UAB_EXT);
    }

    private static string getAssetLoadPath(string assetpath, string loadpath) {
        int idx = assetpath.IndexOf(loadpath, System.StringComparison.OrdinalIgnoreCase);
        return Path.ChangeExtension(assetpath.Substring(idx), null);
    }
}
