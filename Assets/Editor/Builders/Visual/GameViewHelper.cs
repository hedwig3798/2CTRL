using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Builders.Visual
{
    /// <summary>
    /// 캡처 확인용: Game 뷰 해상도(고정) 설정
    /// </summary>
    public static class GameViewHelper
    {
        public static string SetSize(int _w, int _h)
        {
            Type sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object inst = singleton.GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(inst, new object[] { 0 });
            Type groupType = group.GetType();
            Type sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
            Type enumType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, Enum.ToObject(enumType, 1), _w, _h, "CT_" + _w + "x" + _h);
            int idx = -1;
            int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            for (int i = 0; i < count; ++i)
            {
                object s = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((string)sizeType.GetProperty("baseText").GetValue(s) == "CT_" + _w + "x" + _h) { idx = i; break; }
            }
            if (idx < 0)
            {
                groupType.GetMethod("AddCustomSize").Invoke(group, new object[] { size });
                idx = count;
            }
            Type gv = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow win = EditorWindow.GetWindow(gv);
            gv.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(win, new object[] { idx, null });
            win.Repaint();
            return "set " + _w + "x" + _h + " idx " + idx;
        }
    }
}
