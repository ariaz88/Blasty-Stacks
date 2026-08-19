#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoardGhostMask))]
public class BoardGhostMaskEditor : Editor
{
    private const int CellPx = 20;

    public override void OnInspectorGUI()
    {
        var mask = (BoardGhostMask)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Ghost Mask Painter", EditorStyles.boldLabel);

        // Default inspector for refs/flags
        DrawDefaultInspector();

        EditorGUILayout.Space(6);

        // Control buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Resize to Board"))
        {
            Undo.RecordObject(mask, "Resize Ghost Mask");
            mask.ResizeMaskToBoard();
            EditorUtility.SetDirty(mask);
        }
        if (GUILayout.Button("Apply Now"))
        {
            mask.ApplyGhostMask();
        }
        if (GUILayout.Button("Clear All"))
        {
            Undo.RecordObject(mask, "Clear Ghost Mask");
            mask.ClearMask();
            EditorUtility.SetDirty(mask);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Draw the grid
        var board = GetBoard(mask);
        if (!board)
        {
            EditorGUILayout.HelpBox("Assign a BoardGridXZ to draw the mask grid.", MessageType.Info);
            return;
        }

        int w = Mathf.Max(1, board.Width);
        int h = Mathf.Max(1, board.Height);

        var area = GUILayoutUtility.GetRect(w * CellPx + 12, h * CellPx + 12, GUILayout.ExpandWidth(false));
        var rect = new Rect(area.x + 6, area.y + 6, w * CellPx, h * CellPx);
        EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.05f));

        // Y from top to bottom for a natural board look
        for (int y = h - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(w * CellPx + 12));
            GUILayout.Space(6);

            for (int x = 0; x < w; x++)
            {
                bool val = mask.GetCell(x, y);
                var style = GUI.skin.toggle;
                var cellRect = GUILayoutUtility.GetRect(CellPx, CellPx, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

                // Draw border
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, 1), Color.gray * 0.8f);
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.yMax - 1, cellRect.width, 1), Color.gray * 0.8f);
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, 1, cellRect.height), Color.gray * 0.8f);
                EditorGUI.DrawRect(new Rect(cellRect.xMax - 1, cellRect.y, 1, cellRect.height), Color.gray * 0.8f);

                bool newVal = GUI.Toggle(cellRect, val, GUIContent.none, style);
                if (newVal != val)
                {
                    Undo.RecordObject(mask, "Toggle Ghost Cell");
                    mask.SetCell(x, y, newVal);
                    EditorUtility.SetDirty(mask);
                }
            }

            GUILayout.Space(6);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Size: {w} × {h}   (Checked = ghosted{(ShowAlsoBlocked(mask) ? " + blocked" : "")})",
                                   EditorStyles.miniLabel);
    }

    private BoardGridXY GetBoard(BoardGhostMask m)
    {
        var so = serializedObject;
        var prop = so.FindProperty("board");
        return prop != null ? (BoardGridXY)prop.objectReferenceValue : null;
    }

    private bool ShowAlsoBlocked(BoardGhostMask m)
    {
        var so = serializedObject;
        var prop = so.FindProperty("alsoMarkBlocked");
        return prop != null && prop.boolValue;
    }
}
#endif
