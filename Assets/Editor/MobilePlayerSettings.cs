using UnityEditor;
using UnityEngine;

namespace OpenNinja.EditorSetup
{
    /// <summary>
    /// One-shot Editor utility that switches the project's PlayerSettings to a
    /// portrait-mobile profile. Run once via the menu or via the MCP execute path:
    ///   filePath: Assets/Editor/MobilePlayerSettings.cs
    ///   methodName: Apply
    /// Idempotent.
    /// </summary>
    public static class MobilePlayerSettings
    {
        public static string Apply()
        {
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1080;
            PlayerSettings.defaultScreenHeight = 1920;
            // Unity 6 removed the standalone `defaultScreenOrientation` API; the
            // four autorotate flags below pin the runtime orientation to portrait.
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            AssetDatabase.SaveAssets();
            return "PlayerSettings → Portrait 1080x1920";
        }
    }
}
