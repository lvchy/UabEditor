using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class UabEditorDef {
    public static readonly string UabRoot = UabFileUtil.PathCombine(Application.dataPath, "..", UabDef.UAB_ROOT, UabCommon.GetPlatformName());
    public static readonly string DeployRoot = UabFileUtil.PathCombine(Application.dataPath, "..", "deploy", UabCommon.GetPlatformName());
}
