using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OpenNinja.EditorSetup
{
    /// <summary>
    /// One-shot Editor utility that adds (if missing) a "1080x1920 Portrait"
    /// Game View size to the Standalone group and switches the active Game
    /// View to it. Uses reflection because the relevant Unity APIs are
    /// internal. Safe to re-run.
    /// </summary>
    public static class SetPortraitGameView
    {
        private const string SizeName = "1080x1920 Portrait";
        private const int Width = 1080;
        private const int Height = 1920;

        public static string Apply()
        {
            var (group, idx) = EnsureSize();
            ApplyToActiveGameView(group, idx);
            return $"Game View size set to '{SizeName}' ({Width}x{Height})";
        }

        private static (object group, int index) EnsureSize()
        {
            // GameViewSizes.instance.GetGroup(GameViewSizeGroupType.Standalone)
            var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instanceProp = singleType.GetProperty("instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            var gameViewSizes = instanceProp.GetValue(null);

            var getGroup = sizesType.GetMethod("GetGroup");
            var group = getGroup.Invoke(gameViewSizes, new object[] { (int)GameViewSizeGroupType.Standalone });

            // Check if a size with our display text already exists.
            var groupType = group.GetType();
            var getDisplayTexts = groupType.GetMethod("GetDisplayTexts");
            var displayTexts = (string[])getDisplayTexts.Invoke(group, null);
            for (int i = 0; i < displayTexts.Length; i++)
            {
                if (displayTexts[i].StartsWith(SizeName))
                    return (group, i);
            }

            // Add a new fixed-resolution GameViewSize.
            var gameViewSizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
            var gameViewSizeTypeEnum = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
            var fixedRes = Enum.Parse(gameViewSizeTypeEnum, "FixedResolution");

            var ctor = gameViewSizeType.GetConstructor(new[] {
                gameViewSizeTypeEnum, typeof(int), typeof(int), typeof(string)
            });
            var newSize = ctor.Invoke(new object[] { fixedRes, Width, Height, SizeName });

            var addCustomSize = groupType.GetMethod("AddCustomSize");
            addCustomSize.Invoke(group, new[] { newSize });

            // Re-fetch display texts to find the new index.
            displayTexts = (string[])getDisplayTexts.Invoke(group, null);
            int newIdx = Array.FindIndex(displayTexts, s => s.StartsWith(SizeName));
            return (group, newIdx >= 0 ? newIdx : 0);
        }

        private static void ApplyToActiveGameView(object group, int index)
        {
            var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            var window = EditorWindow.GetWindow(gameViewType, false, "Game", false);
            if (window == null) return;

            var sizeSelectionCallback = gameViewType.GetMethod("SizeSelectionCallback",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (sizeSelectionCallback != null)
            {
                sizeSelectionCallback.Invoke(window, new object[] { index, null });
            }
            else
            {
                // Fallback: set the property directly.
                var selectedSizeIndex = gameViewType.GetProperty("selectedSizeIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                selectedSizeIndex?.SetValue(window, index);
            }
            window.Repaint();
        }
    }
}
