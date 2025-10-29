using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TreeEditor : EditorWindow
{
    private static readonly int Cull = Shader.PropertyToID("_Cull");
    
    private Mesh mesh;
    private Material barkMat;
    private Material leavesMat;
    
    //Mesh viewer
    private PreviewRenderUtility previewUtility;
    private Vector2 orbitAngles = new Vector2(120, -20);
    private float distance = 3f;
    private Vector3 pivot = Vector3.zero;

    // --- Splitter (resizable column) ---
    private float splitterPos = 300f;     // starting width of left column
    private bool isResizing = false;
    private const float splitterWidth = 4f;
    
    //Properties
    private Vector2 scrollPos;
    
    #region TreeOptions
    
    private ProceduralTreeParameters loadedParameters;
    
    private int seed = 0;
    private int level = 0;
    
    //Branches
    public List<int> branches = new() { 0 };
    public List<float> lengths = new() { 40 };
    public List<float> radius = new() { 2 };
    public List<float> thinning = new() { 0.7f };
    public List<Vector2> childBranchEmergencePos = new();
    public List<float> angle = new();
    public List<float> gnarliness = new() { 10f };
    public Growth growth;
    public List<int> sectionCount = new() { 12 };
    public List<int> meshSegmentCount = new() { 16 };
    
    //Leaves
    public bool generateLeaves = true;
    public int leavesCount = 35;
    public Vector2 leavesEmergenceStartEnd = new Vector2(0, 1);
    public float leavesAngle = 35f;
    public float leafSize = 4;
    public float leafSizeVariance = 0.7f;
    
    #endregion
    
    [MenuItem("Window/Tree Editor")]
    static void Init()
        => GetWindow<TreeEditor>("Tree Editor");

    void OnEnable()
    {
        previewUtility = new PreviewRenderUtility();
        previewUtility.cameraFieldOfView = 30f;
        splitterPos = position.width;
        mesh = new Mesh();
        GenerateMesh();
    }

    void OnDisable()
    {
        previewUtility.Cleanup();
    }

    void OnGUI()
    {
        float totalWidth = position.width;
        float rightWidth = Mathf.Max(120, totalWidth - splitterPos - splitterWidth);

        Rect leftRect = new Rect(0, 0, splitterPos, position.height);
        Rect splitterRect = new Rect(splitterPos, 0, splitterWidth, position.height);
        Rect rightRect = new Rect(splitterPos + splitterWidth, 0, rightWidth, position.height);
        
        GUILayout.BeginArea(leftRect);
        
        // --- Preview Rect ---
        Rect previewRect = GUILayoutUtility.GetRect(256, 256, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        HandlePreviewInput(previewRect);
        DrawMeshPreview(previewRect);
        GUILayout.EndArea();

        // --- Splitter handle (drag area) ---
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
        HandleResize(splitterRect, totalWidth);
        
        
        GUILayout.BeginArea(rightRect);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos,GUILayout.Height(position.height));
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        {
            if (GUILayout.Button("Generate Tree", GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                GenerateMesh();
            
            GUILayout.Space(16);
            
            TreeOptions();
            
            GUILayout.Space(16);
            
            // --- Save Button ---
            EditorGUILayout.LabelField("Save Settings", EditorStyles.boldLabel);
            if (GUILayout.Button("💾 Save Mesh", GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                SaveMeshAsset();
            
            GUILayout.Space(8);
            if (GUILayout.Button(loadedParameters ? loadedParameters.name : "Load Preset",
                    GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                int pickerControlID = GUIUtility.GetControlID(FocusType.Passive);
                EditorGUIUtility.ShowObjectPicker<ProceduralTreeParameters>(null, false, "", pickerControlID);
            }
            
            if (GUILayout.Button("Save to loaded Preset", GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                SavePreset();
            
            if (GUILayout.Button("Create Preset", GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                CreatePreset();
            
            LoadPreset();
        }
        EditorGUILayout.EndVertical();
        GUILayout.EndArea();
        
        EditorGUILayout.EndScrollView();
        
    }

    // --- Splitter Drag Logic ---
    void HandleResize(Rect splitterRect, float totalWidth)
    {
        Event e = Event.current;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (splitterRect.Contains(e.mousePosition))
                {
                    isResizing = true;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (isResizing)
                {
                    splitterPos = Mathf.Clamp(e.mousePosition.x, 100f, totalWidth - 120f);
                    Repaint();
                }
                break;

            case EventType.MouseUp:
                if (isResizing)
                {
                    isResizing = false;
                    e.Use();
                }
                break;
        }
    }
    
    void HandlePreviewInput(Rect rect)
    {
        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;

        if (!rect.Contains(e.mousePosition)) return;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0 || e.button == 1)
                {
                    GUIUtility.hotControl = controlID;
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlID)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlID)
                {
                    if (e.button == 0)
                    {
                        orbitAngles.x += e.delta.x;
                        orbitAngles.y -= e.delta.y;
                        orbitAngles.y = Mathf.Clamp(orbitAngles.y, -85f, 85f);
                    }
                    else if (e.button == 1)
                    {
                        float panSpeed = 0.002f * distance;
                        Vector3 right = Quaternion.Euler(0, orbitAngles.x, 0) * Vector3.right;
                        Vector3 up = Vector3.up;
                        pivot -= (right * e.delta.x + up * e.delta.y) * panSpeed;
                    }
                    e.Use();
                }
                break;

            case EventType.ScrollWheel:
                float zoomDelta = e.delta.y * 0.5f;
                distance +=zoomDelta;
                e.Use();
                break;
        }
    }

    void DrawMeshPreview(Rect rect)
    {
        if (Event.current.type != EventType.Repaint) return;

        previewUtility.BeginPreview(rect, GUIStyle.none);

        // --- Camera setup ---
        Quaternion rot = Quaternion.Euler(orbitAngles.y, orbitAngles.x, 0);
        Vector3 camPos = pivot + rot * (Vector3.back * distance);

        previewUtility.camera.transform.position = camPos;
        previewUtility.camera.transform.rotation = rot;
        previewUtility.camera.nearClipPlane = 0.01f;
        previewUtility.camera.farClipPlane = 1000f;

        // --- Lighting setup ---
        previewUtility.lights[0].intensity = 1.4f;
        previewUtility.lights[0].transform.rotation = Quaternion.Euler(50f, 50f, 0);
        previewUtility.lights[1].intensity = 1.4f;

        // --- Draw mesh ---
        Material barkMatToUse = barkMat ?? new Material(Shader.Find("Standard"));
        Material leavesMatToUse = leavesMat ?? new Material(Shader.Find("Standard"));
        previewUtility.DrawMesh(mesh, Matrix4x4.identity, barkMatToUse, 0);
        previewUtility.DrawMesh(mesh, Matrix4x4.identity, leavesMatToUse, 1);
        previewUtility.camera.Render();

        // --- Draw final texture ---
        Texture result = previewUtility.EndPreview();
        GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);
    }
    
    void TreeOptions()
    {
        seed = EditorGUILayout.IntField("Seed", seed);
        
        level = EditorGUILayout.IntField("Level", level);
        
        GUILayout.Space(8);
        
        EditorGUILayout.LabelField("Branches Settings", EditorStyles.boldLabel);
        barkMat = (Material)EditorGUILayout.ObjectField("Bark Material", barkMat, typeof(Material), false);
        
        GUILayout.Space(4);
        
        SerializedObject so = new SerializedObject(this);
        EditorGUILayout.PropertyField(so.FindProperty("branches"), true);
        EditorGUILayout.PropertyField(so.FindProperty("lengths"), true);
        EditorGUILayout.PropertyField(so.FindProperty("radius"), true);
        EditorGUILayout.PropertyField(so.FindProperty("thinning"), true);
        EditorGUILayout.PropertyField(so.FindProperty("childBranchEmergencePos"), true);
        EditorGUILayout.PropertyField(so.FindProperty("angle"), true);
        EditorGUILayout.PropertyField(so.FindProperty("gnarliness"), true);
        EditorGUILayout.PropertyField(so.FindProperty("growth"), true);
        EditorGUILayout.PropertyField(so.FindProperty("sectionCount"), true);
        EditorGUILayout.PropertyField(so.FindProperty("meshSegmentCount"), true);
        
        GUILayout.Space(8);
        
        EditorGUILayout.LabelField("Leaves Settings", EditorStyles.boldLabel);
        
        leavesMat = (Material)EditorGUILayout.ObjectField("Leaves Material", leavesMat, typeof(Material), false);
        if (leavesMat)
            leavesMat.SetFloat(Cull, (float)UnityEngine.Rendering.CullMode.Off);

        GUILayout.Space(4);
        
        EditorGUILayout.PropertyField(so.FindProperty("generateLeaves"), true);
        EditorGUILayout.PropertyField(so.FindProperty("leavesCount"), true);
        EditorGUILayout.PropertyField(so.FindProperty("leavesEmergenceStartEnd"), true);
        EditorGUILayout.PropertyField(so.FindProperty("leavesAngle"), true);
        EditorGUILayout.PropertyField(so.FindProperty("leafSize"), true);
        EditorGUILayout.PropertyField(so.FindProperty("leafSizeVariance"), true);
        
        so.ApplyModifiedProperties();

        CorrectOptions();
    }

    void CorrectOptions()
    {
        level = Mathf.Clamp(level, 0, level);
        
        //Check lists lengths
        AddNecessaryListElement(branches, false);
        AddNecessaryListElement(lengths, true);
        AddNecessaryListElement(radius, true, 2f);
        AddNecessaryListElement(thinning, true, 0.7f);
        AddNecessaryListElement(childBranchEmergencePos, true);
        AddNecessaryListElement(angle, false);
        AddNecessaryListElement(gnarliness, true);
        AddNecessaryListElement(sectionCount, true);
        AddNecessaryListElement(meshSegmentCount, true, 3);
        
        for (int i =  0; i < branches.Count; i++)
            branches[i] = Mathf.Clamp(branches[i], 0, branches[i]);
        
        for (int i =  0; i < lengths.Count; i++)
            lengths[i] = Mathf.Clamp(lengths[i], 0, lengths[i]);
        
        for (int i =  0; i < radius.Count; i++)
            radius[i] = Mathf.Clamp(radius[i], 0, radius[i]);
        
        for (int i =  0; i < thinning.Count; i++)
            thinning[i] = Mathf.Clamp(thinning[i], 0, thinning[i]);

        for (int i = 0; i < childBranchEmergencePos.Count; i++)
        {
            float newX = Mathf.Clamp01(childBranchEmergencePos[i].x);
            float newY = Mathf.Clamp01(childBranchEmergencePos[i].y);
            childBranchEmergencePos[i] = new Vector2(newX, newY);
        }

        for (int i = 0; i < angle.Count; i++)
            angle[i] = Mathf.Clamp(angle[i], 0, 360);

        for (int i = 0; i < gnarliness.Count; i++)
            gnarliness[i] = Mathf.Clamp(gnarliness[i], -30, 30);

        for (int i = 0; i < sectionCount.Count; i++)
            sectionCount[i] = Mathf.Clamp(sectionCount[i], 0, sectionCount[i]);

        for (int i = 0; i < meshSegmentCount.Count; i++)
            meshSegmentCount[i] = Mathf.Clamp(meshSegmentCount[i], 3, meshSegmentCount[i]);
        
        
        leavesCount = Mathf.Clamp(leavesCount, 0, leavesCount);
        
        leavesEmergenceStartEnd.x = Mathf.Clamp01(leavesEmergenceStartEnd.x);
        leavesEmergenceStartEnd.y = Mathf.Clamp01(leavesEmergenceStartEnd.y);
        
        leavesAngle = Mathf.Clamp(leavesAngle, 0, 360);
        
        leafSizeVariance = Mathf.Clamp01(leafSizeVariance);
    }

    void AddNecessaryListElement<T>(List<T> list, bool equalToLevel, T defaultValue = default)
    {
        if (list.Count >= level + (equalToLevel ? 1 : 0)) 
            return;
        
        int toAdd = level + (equalToLevel ? 1 : 0) - list.Count;
        for (int i = 0; i < toAdd; i++)
            list.Add(defaultValue);
    }
    
    void GenerateMesh()
    {
        TreeParameters parameters = new TreeParameters
        {
            seed = seed,
            level = level,
            branches = branches,
            lengths = lengths,
            radius = radius,
            thinning = thinning,
            childBranchEmergencePos = childBranchEmergencePos,
            angle = angle,
            gnarliness = gnarliness,
            growth = growth,
            sectionCount = sectionCount,
            meshSegmentCount = meshSegmentCount,
            
            generateLeaves = generateLeaves,
            leavesCount = leavesCount,
            leavesEmergenceStartEnd = leavesEmergenceStartEnd,
            leavesAngle = leavesAngle,
            leafSize = leafSize,
            leafSizeVariance = leafSizeVariance,
        };
        
        TreeMeshData meshData = ProceduralTreeGeneration.GenerateTree(parameters);
        mesh.Clear();
        mesh.vertices = meshData.vertices;
        mesh.uv = meshData.uvs;
        
        mesh.subMeshCount = 2;
        mesh.SetTriangles(meshData.barkIndices, 0);
        mesh.SetTriangles(meshData.leavesIndices, 1);
        mesh.RecalculateNormals();
    }
    
    void SaveMeshAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Mesh As Asset", 
            "NewMesh.asset",
            "asset",
            "Choose a location to save the mesh."
        );

        if (string.IsNullOrEmpty(path))
            return; // user cancelled

        // Duplicate mesh so we don’t modify the original asset
        Mesh meshCopy = Instantiate(mesh);
        meshCopy.name = mesh.name;

        AssetDatabase.CreateAsset(meshCopy, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Mesh Saved", $"Saved mesh as:\n{path}", "OK");
    }

    void LoadPreset()
    {
        string command = Event.current.commandName;
        if (command != "ObjectSelectorClosed" || EditorGUIUtility.GetObjectPickerObject() is not ProceduralTreeParameters)
            return;
        
        loadedParameters = EditorGUIUtility.GetObjectPickerObject() as ProceduralTreeParameters;
        Repaint();
        
        if (!loadedParameters)
            return;

        seed = loadedParameters.Seed;
        level = loadedParameters.TreeLevels;
        
        barkMat = loadedParameters.BarkMat;
        branches = loadedParameters.ChildBranchesCounts;
        lengths = loadedParameters.BranchLength;
        radius = loadedParameters.BranchRadius;
        thinning = loadedParameters.Thinning;
        childBranchEmergencePos = loadedParameters.ChildBranchEmergencePos;
        angle = loadedParameters.Angle;
        gnarliness = loadedParameters.Gnarliness;
        growth = loadedParameters.TreeGrowth;
        sectionCount = loadedParameters.BranchSectionCount;
        meshSegmentCount = loadedParameters.MeshSegmentCount;
    
        leavesMat = loadedParameters.LeavesMat;
        generateLeaves = loadedParameters.GenerateLeaves;
        leavesCount = loadedParameters.LeavesCount;
        leavesEmergenceStartEnd = loadedParameters.LeavesEmergenceStartEnd;
        leavesAngle = loadedParameters.LeavesAngle;
        leafSize = loadedParameters.LeafSize;
        leafSizeVariance = loadedParameters.LeafSizeVariance;
        
        GenerateMesh();
    }

    private void SaveToScriptableObject(ProceduralTreeParameters saveInto)
    {
        saveInto.Seed = seed;
        saveInto.TreeLevels = level;
        
        saveInto.BarkMat = barkMat;
        saveInto.ChildBranchesCounts = branches;
        saveInto.BranchLength = lengths;
        saveInto.BranchRadius = radius;
        saveInto.BranchSectionCount = sectionCount;
        saveInto.MeshSegmentCount = meshSegmentCount;
        saveInto.Gnarliness = gnarliness;
        saveInto.TreeGrowth = growth;
        saveInto.Angle = angle;
        saveInto.Thinning = thinning;
        saveInto.ChildBranchEmergencePos = childBranchEmergencePos;
        
        saveInto.LeavesMat = leavesMat;
        saveInto.GenerateLeaves = generateLeaves;
        saveInto.LeavesCount = leavesCount;
        saveInto.LeavesEmergenceStartEnd = leavesEmergenceStartEnd;
        saveInto.LeavesAngle = leavesAngle;
        saveInto.LeafSize = leafSize;
        saveInto.LeafSizeVariance = leafSizeVariance;
    }
    
    void SavePreset()
    {
        if (!loadedParameters)
            return;

        SaveToScriptableObject(loadedParameters);
    }

    void CreatePreset()
    {
        ProceduralTreeParameters asset = CreateInstance<ProceduralTreeParameters>();
        SaveToScriptableObject(asset);
        
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Tree Parameters",
            "TreeParameters.asset",
            "asset",
            "Choose a location to save the asset"
        );

        if (string.IsNullOrEmpty(path))
            return;
        
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;
    }
}
