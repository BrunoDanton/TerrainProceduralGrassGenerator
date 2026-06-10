using UnityEngine;
using UnityEditor;

/// <summary>
/// Painel customizado para o Unity Editor responsável por desenhar a interface organizada do gerador.
/// </summary>
[CustomEditor(typeof(GrassGenerationController))]
public class TerrainGrassGeneratorEditor : Editor
{
    private GrassGenerationController _targetController;
    private bool _isAdvancedSettingsFoldoutOpen;

    private void OnEnable()
    {
        _targetController = (GrassGenerationController)target;
        // Carrega o estado aberto/fechado anterior da subseção avançada
        _isAdvancedSettingsFoldoutOpen = EditorPrefs.GetBool("GrassEditor_AdvancedFoldout", false);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawGrassHeader();
        EditorGUILayout.Space(10);

        DrawQuickActions();
        EditorGUILayout.Space(15);

        // --- SEÇÕES DO INSPECTOR ---
        
        // 1. Configurações Base
        EditorGUILayout.LabelField("🗺️ Configurações Base", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_grassLayersList"), new GUIContent("Camadas do Terreno"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_bladeTypesList"), new GUIContent("Tipos de Lâminas"), true);
        
        EditorGUILayout.Space(10);
        
        // 2. Densidade
        EditorGUILayout.LabelField("📐 Densidade e Posicionamento", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_chunkSize"), new GUIContent("Tamanho do Chunk"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_grassDensity"), new GUIContent("Densidade de Lâminas"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_leafDispersion"), new GUIContent("Dispersão das Lâminas"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxVerticesPerChunk"), new GUIContent("Limite de Vértices/Chunk"));
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(10);

        // 3. Distribuição e Ruído
        EditorGUILayout.LabelField("🎲 Distribuição e Ruído (Clumping)", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_perlinNoiseScale"), new GUIContent("Escala do Perlin Noise"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_clumpingScale"), new GUIContent("Escala de Clumping (Voronoi)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_minimumAcceptableNoise"), new GUIContent("Corte de Ruído (Threshold)"));
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(10);
        
        // 4. Culling
        EditorGUILayout.LabelField("✂️ Culling e Performance", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxRenderDistance"), new GUIContent("Distância Máxima"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_shouldUseFrustumCulling"), new GUIContent("Usar Frustum Culling"));
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(10);

        // 5. Vento e Interação
        EditorGUILayout.LabelField("💨 Vento e Interação", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        
        SerializedProperty isWindEnabledProp = serializedObject.FindProperty("_isWindEnabled");
        EditorGUILayout.PropertyField(isWindEnabledProp, new GUIContent("Habilitar Vento"));
        
        if (isWindEnabledProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_windSpeed"), new GUIContent("Velocidade do Vento"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_windStrength"), new GUIContent("Força do Vento"));
            EditorGUILayout.Slider(serializedObject.FindProperty("_windDirection"), 0f, 360f, new GUIContent("Direção (Graus)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_windTurbulence"), new GUIContent("Turbulência"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(5);

        SerializedProperty canInteractProp = serializedObject.FindProperty("_canInteract");
        EditorGUILayout.PropertyField(canInteractProp, new GUIContent("Habilitar Interação"));
        
        if (canInteractProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_playerTransform"), new GUIContent("Transform do Jogador"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_interactionRadius"), new GUIContent("Raio de Interação"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_interactionStrength"), new GUIContent("Força da Interação"));
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(10);

        // 6. Materiais
        EditorGUILayout.LabelField("🎨 Materiais", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_grassMaterial"), new GUIContent("Material da Grama"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_interactionFadeMaterial"), new GUIContent("Material de Fade"));
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(15);

        // --- SUBSECÇÃO: CONFIGURAÇÕES AVANÇADAS (FOLDOUT) ---
        bool previousFoldoutState = _isAdvancedSettingsFoldoutOpen;
        _isAdvancedSettingsFoldoutOpen = EditorGUILayout.Foldout(_isAdvancedSettingsFoldoutOpen, "🛠️ Configurações Avançadas (Engine)", true, EditorStyles.foldoutHeader);
        
        if (_isAdvancedSettingsFoldoutOpen)
        {
            if (previousFoldoutState != _isAdvancedSettingsFoldoutOpen)
                EditorPrefs.SetBool("GrassEditor_AdvancedFoldout", _isAdvancedSettingsFoldoutOpen);

            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(serializedObject.FindProperty("_terrainNormalBlend"), 0f, 1f, new GUIContent("Alinhamento com Encosta", "0 = Totalmente Vertical, 1 = Alinhado à Inclinação do Terreno"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_minScaleMultiplier"), new GUIContent("Escala MÍNIMA da Grama"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxScaleMultiplier"), new GUIContent("Escala MÁXIMA da Grama"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_frustumPadding"), new GUIContent("Margem do Frustum (Culling)", "Evita que a grama pisque nas bordas da câmera"));
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawGrassHeader()
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };

        EditorGUILayout.LabelField("Grass Generator", headerStyle);
        
        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.gray }
        };
        
        EditorGUILayout.LabelField("Instancia e Personaliza por Conta Própria", subtitleStyle);
    }

    private void DrawQuickActions()
    {
        EditorGUILayout.BeginHorizontal();
        
        GUI.backgroundColor = Color.dimGray;
        if (GUILayout.Button("Gerar Grama!", GUILayout.Height(40)))
        {
            _targetController.GenerateGrass();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
    }
}