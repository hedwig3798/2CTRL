using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TileData))]
public class TileDataEditor
    : Editor
{
    private string[] directionName = new string[]
    {
        "Up Socket"
        , "Down Socket"
        , "Left Socket"
        , "Right Socket"
    };

    public override void OnInspectorGUI()
    {
        TileData tileData = (TileData)target;

        EditorGUI.BeginChangeCheck();
        tileData.weight = EditorGUILayout.FloatField("Weight", tileData.weight);
        if (true == EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(tileData);
        }

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        tileData.tileSet = (TileSet)EditorGUILayout.ObjectField
        (
            "Target TileSet"
            , tileData.tileSet
            , typeof(TileSet)
            , false
        );

        if (true == EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(tileData);
        }

        if (null == tileData.tileSet)
        {
            EditorGUILayout.HelpBox("TileSet 을 설정해주세요", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Direction Sockets", EditorStyles.boldLabel);

        TileSocket[] sockets = tileData.tileSet.sockets;
        string[] popupOptions = new string[sockets.Length + 1];
        popupOptions[0] = "None";
        for (int i = 0; i < sockets.Length; ++i)
        {
            if (null == sockets[i])
            {
                popupOptions[i + 1] = "Null";
            }
            else
            {
                popupOptions[i + 1] = sockets[i].name;
            }
        }
        EditorGUI.BeginChangeCheck();

        tileData.upSocket = SocketPopup(directionName[0], tileData.upSocket, sockets, popupOptions);
        tileData.downSocket = SocketPopup(directionName[1], tileData.downSocket, sockets, popupOptions);
        tileData.leftSocket = SocketPopup(directionName[2], tileData.leftSocket, sockets, popupOptions);
        tileData.rightSocket = SocketPopup(directionName[3], tileData.rightSocket, sockets, popupOptions);

        if (true == EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(tileData, "Change Socket Data");
            EditorUtility.SetDirty(tileData);
        }
    }

    private TileSocket SocketPopup(string _name, TileSocket _currSocket, TileSocket[] _tileSockets, string[] _popupOptions)
    {
        int currentIndex = 0;

        // 현재 할당된 소켓 에셋이 배열의 몇 번째 인덱스인지 탐색
        if (null != _currSocket)
        {
            for (int i = 0; i < _tileSockets.Length; ++i)
            {
                if (true == (_tileSockets[i] == _currSocket))
                {
                    currentIndex = i + 1;
                    break;
                }
            }
        }

        // 인스펙터에 팝업 메뉴 출력
        int selectedIndex = EditorGUILayout.Popup(_name, currentIndex, _popupOptions);

        // 0번(None) 선택 시 null 반환
        if (0 == selectedIndex)
        {
            return null;
        }

        // 선택된 인덱스에 매핑되는 소켓 SO 반환
        return _tileSockets[selectedIndex - 1];
    }
}
