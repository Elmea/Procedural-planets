using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class ChecklistWindow : EditorWindow
{
    private static string checklistPath = "Assets/Checklist/checklist.json";
    private ChecklistData checklistData;

    [MenuItem("Tools/Checklist")]
    public static void ShowWindow()
    {
        GetWindow<ChecklistWindow>("Project Checklist");
    }

    private void OnEnable()
    {
        LoadChecklist();
    }

    private void OnGUI()
    {
        if (checklistData == null)
        {
            if (GUILayout.Button("Create Checklist"))
            {
                checklistData = new ChecklistData();
                SaveChecklist();
            }
            return;
        }

        EditorGUILayout.LabelField("Checklist", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        for (int i = 0; i < checklistData.items.Count; i++)
        {
            DrawItem(checklistData.items[i], 0, () => checklistData.items.RemoveAt(i));
        }

        if (GUILayout.Button("Ajouter une t�che"))
        {
            checklistData.items.Add(new ChecklistItem("Nouvelle t�che"));
        }

        if (EditorGUI.EndChangeCheck())
        {
            SaveChecklist();
        }

        if (GUILayout.Button("Recharger"))
        {
            LoadChecklist();
        }
    }

    private void DrawItem(ChecklistItem item, int indent, System.Action onDelete)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(indent * 20);

        item.isDone = EditorGUILayout.Toggle(item.isDone, GUILayout.Width(20));
        item.text = EditorGUILayout.TextField(item.text);

        if (GUILayout.Button("+", GUILayout.Width(25)))
        {
            item.subTasks.Add(new ChecklistItem("Nouvelle sous-t�che"));
            SaveChecklist();
        }

        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            onDelete?.Invoke();
            SaveChecklist();
            EditorGUILayout.EndHorizontal();
            return;
        }

        EditorGUILayout.EndHorizontal();

        // Dessin r�cursif des sous-t�ches
        for (int i = 0; i < item.subTasks.Count; i++)
        {
            DrawItem(item.subTasks[i], indent + 1, () => item.subTasks.RemoveAt(i));
        }
    }

    private void LoadChecklist()
    {
        if (File.Exists(checklistPath))
        {
            string json = File.ReadAllText(checklistPath);
            checklistData = JsonUtility.FromJson<ChecklistData>(json);
        }
        else
        {
            checklistData = new ChecklistData();
        }
    }

    private void SaveChecklist()
    {
        string dir = Path.GetDirectoryName(checklistPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string json = JsonUtility.ToJson(checklistData, true);
        File.WriteAllText(checklistPath, json);
        AssetDatabase.Refresh();
    }
}

[System.Serializable]
public class ChecklistData
{
    public List<ChecklistItem> items = new List<ChecklistItem>();
}

[System.Serializable]
public class ChecklistItem
{
    public string text;
    public bool isDone;
    public List<ChecklistItem> subTasks = new List<ChecklistItem>();

    public ChecklistItem(string text)
    {
        this.text = text;
        this.isDone = false;
        this.subTasks = new List<ChecklistItem>();
    }
}