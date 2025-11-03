using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(TerrainGrassGenerator))]
public class TerrainGrassGeneratorEditor : Editor
{
    private TerrainGrassGenerator _target;
    private bool _showTerrainSettings = true;
    private bool _showDensitySettings = true;
    private bool _showBladeTypes = true;
    private bool _showCullingSettings = true;
    private bool _showWindSettings = true;
    private bool _showInteractionSettings = true;
    private bool _showOptimizationSettings = true;
    private bool _showMaterialSettings = true;
    private bool _showDebugTools = true;

    private readonly string[] _presetNames = new string[]
    {
        "Grama Comum",
        "Grama Alta",
        "Grama Baixa",
        "Trevo",
        "Capim",
        "Custom"
    };

    private void OnEnable()
    {
        _target = (TerrainGrassGenerator)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeader();
        EditorGUILayout.Space(10);

        DrawQuickActions();
        EditorGUILayout.Space(15);

        DrawTerrainSection();
        DrawDensitySection();
        DrawBladeTypesSection();
        DrawCullingSection();
        DrawWindSection();
        DrawInteractionSection();
        DrawOptimizationSection();
        DrawMaterialSection();
        DrawDebugSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader()
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.3f, 0.8f, 0.3f) }
        };

        EditorGUILayout.LabelField("🌾 AAA Grass Generator 🌾", headerStyle);
        
        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.gray }
        };
        
        EditorGUILayout.LabelField("Ghost of Tsushima • Breath of the Wild • Horizon Zero Dawn", subtitleStyle);
    }

    private void DrawQuickActions()
    {
        EditorGUILayout.BeginHorizontal();
        
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("🌱 Gerar Grama", GUILayout.Height(40)))
        {
            _target.GenerateGrass();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("📊 Estatísticas", GUILayout.Height(30)))
        {
            _target.ShowPerformanceStats();
        }

        if (GUILayout.Button("🔧 Otimizar Material", GUILayout.Height(30)))
        {
            _target.OptimizeMaterialForPerformance();
        }

        if (GUILayout.Button("🧪 Testar Shader", GUILayout.Height(30)))
        {
            _target.TestWindShader();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTerrainSection()
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("grassLayers"), new GUIContent("🗺️ Camadas do Terreno"), true);

        _showTerrainSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showTerrainSettings, "   ↳ Configurações de Terreno e Ruído");
        
        if (_showTerrainSettings)
        {
            EditorGUI.indentLevel++;
            DrawPropertyWithReset("minimumTextureWeight", "Peso Mínimo da Textura");
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Ruído Perlin", EditorStyles.boldLabel);
            DrawPropertyWithReset("perlinNoiseScale", "Escala do Ruído");
            DrawPropertyWithReset("minimumNoiseAcceptableValue", "Valor Mínimo Aceito");
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawDensitySection()
    {
        _showDensitySettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showDensitySettings, "📐 Densidade e Posicionamento");
        
        if (_showDensitySettings)
        {
            EditorGUI.indentLevel++;
            DrawPropertyWithReset("chunkSize", "Tamanho do Chunk");
            DrawPropertyWithReset("grassDensity", "Densidade de Lâminas");
            DrawPropertyWithReset("leafDispersion", "Dispersão das Lâminas");
            
            EditorGUILayout.HelpBox(
                $"Chunks: ~{Mathf.CeilToInt(500f / _target.chunkSize)}x{Mathf.CeilToInt(500f / _target.chunkSize)} para terreno 500x500\n" +
                $"Lâminas por chunk: ~{_target.chunkSize * _target.chunkSize * _target.grassDensity}",
                MessageType.Info
            );
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawBladeTypesSection()
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bladeTypes"), new GUIContent("🌿 Tipos de Lâminas"), true);

        _showBladeTypes = EditorGUILayout.BeginFoldoutHeaderGroup(_showBladeTypes, "   ↳ Configurações de Lâminas");
        
        if (_showBladeTypes)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Presets Rápidos:", GUILayout.Width(100));
            
            if (GUILayout.Button("+ Grama Comum"))
                AddBladePreset(0);
            if (GUILayout.Button("+ Grama Alta"))
                AddBladePreset(1);
            if (GUILayout.Button("+ Trevo"))
                AddBladePreset(3);
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Controle de Distribuição", EditorStyles.boldLabel);
            DrawPropertyWithReset("useNoiseForBladeTypes", "Usar Ruído para Tipos");
            DrawPropertyWithReset("bladeTypeNoiseScale", "Escala do Ruído de Tipos");
            DrawPropertyWithReset("smoothTypeTransitions", "Suavizar Transições");
            DrawPropertyWithReset("transitionWidth", "Largura da Transição");
            
            if (_target.bladeTypes != null && _target.bladeTypes.Count > 0)
            {
                EditorGUILayout.Space(5);
                DrawNoiseRangeVisualization();
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawCullingSection()
    {
        _showCullingSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showCullingSettings, "✂️ Culling e LOD");
        
        if (_showCullingSettings)
        {
            EditorGUI.indentLevel++;
            DrawPropertyWithReset("maxRenderDistance", "Distância Máxima");
            DrawPropertyWithReset("lod0Distance", "LOD0 (Alta Qualidade)");
            DrawPropertyWithReset("lod1Distance", "LOD1 (Média Qualidade)");
            DrawPropertyWithReset("cullPercentage", "Percentual de Culling");
            DrawPropertyWithReset("useFrustumCulling", "Usar Frustum Culling");
            
            EditorGUILayout.HelpBox(
                "💡 Dica: Reduza maxRenderDistance para melhorar FPS em cenas grandes.",
                MessageType.Info
            );
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawWindSection()
    {
        _showWindSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showWindSettings, "💨 Animação de Vento");
        
        if (_showWindSettings)
        {
            EditorGUI.indentLevel++;
            DrawPropertyWithReset("enableWind", "Habilitar Vento");
            
            if (_target.enableWind)
            {
                DrawPropertyWithReset("windSpeed", "Velocidade");
                DrawPropertyWithReset("windStrength", "Força");
                DrawPropertyWithReset("windDirection", "Direção (graus)");
                DrawPropertyWithReset("windTurbulence", "Turbulência");
                
                DrawWindDirectionVisual();
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawInteractionSection()
    {
        _showInteractionSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showInteractionSettings, "🎮 Grama Interativa");
        
        if (_showInteractionSettings)
        {
            EditorGUI.indentLevel++;
            DrawPropertyWithReset("enableInteraction", "Habilitar Interação");
            
            if (_target.enableInteraction)
            {
                DrawPropertyWithReset("playerTransform", "Transform do Jogador");
                DrawPropertyWithReset("interactionRadius", "Raio de Interação");
                DrawPropertyWithReset("interactionStrength", "Força da Interação");
                
                if (_target.playerTransform == null)
                {
                    EditorGUILayout.HelpBox(
                        "⚠️ Atribua o Transform do jogador para ativar a interação!",
                        MessageType.Warning
                    );
                }
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawOptimizationSection()
    {
        _showOptimizationSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showOptimizationSettings, "⚡ Otimização Avançada");
        
        if (_showOptimizationSettings)
        {
            EditorGUI.indentLevel++;
            DrawPropertyWithReset("maxVerticesPerChunk", "Vértices por Chunk");
            DrawPropertyWithReset("useAmbientOcclusion", "Usar AO Fake");
            
            if (_target.useAmbientOcclusion)
            {
                DrawPropertyWithReset("aoIntensity", "Intensidade do AO");
            }
            
            DrawPropertyWithReset("randomizeRotation", "Rotação Aleatória");
            DrawPropertyWithReset("heightVariation", "Variação de Altura");
            
            if (_target.heightVariation)
            {
                DrawPropertyWithReset("heightVariationAmount", "Quantidade de Variação");
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawMaterialSection()
    {
        _showMaterialSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showMaterialSettings, "🎨 Material");
        
        if (_showMaterialSettings)
        {
            EditorGUI.indentLevel++;
            DrawPropertyWithReset("grassMaterial", "Material da Grama");
            
            if (_target.grassMaterial == null)
            {
                EditorGUILayout.HelpBox(
                    "❌ Material não atribuído! A grama não será renderizada.",
                    MessageType.Error
                );
            }
            else
            {
                bool hasWindParams = _target.grassMaterial.HasProperty("_WindParams");
                bool hasInteraction = _target.grassMaterial.HasProperty("_InteractionPos");
                
                EditorGUILayout.LabelField("Status do Shader:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"   {(hasWindParams ? "✅" : "❌")} _WindParams (Vento)");
                EditorGUILayout.LabelField($"   {(hasInteraction ? "✅" : "❌")} _InteractionPos (Interação)");
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
    }

    private void DrawDebugSection()
    {
        _showDebugTools = EditorGUILayout.BeginFoldoutHeaderGroup(_showDebugTools, "🔍 Ferramentas de Debug");
        
        if (_showDebugTools)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.LabelField("Visualização", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Gizmos são desenhados na Scene View quando o objeto está selecionado:\n" +
                "🟢 Verde = LOD0 (Qualidade Alta)\n" +
                "🔵 Azul = LOD1 (Qualidade Média)\n" +
                "🟡 Amarelo = LOD2 (Qualidade Baixa)\n" +
                "🔴 Vermelho = Culled (Não Renderizado)",
                MessageType.Info
            );
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Atalhos", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Clique direito no componente → Context Menu:\n" +
                "• Gerar Grama\n" +
                "• Estatísticas de Performance\n" +
                "• Otimizar Material\n" +
                "• Testar Shader",
                MessageType.Info
            );
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPropertyWithReset(string propertyName, string label)
    {
        SerializedProperty prop = serializedObject.FindProperty(propertyName);
        
        if (prop != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
            
            if (prop.propertyType == SerializedPropertyType.Float || 
                prop.propertyType == SerializedPropertyType.Integer)
            {
                if (GUILayout.Button("↺", GUILayout.Width(25)))
                {
                    ResetPropertyToDefault(propertyName, prop);
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }
    }

    private void ResetPropertyToDefault(string propertyName, SerializedProperty prop)
    {
        switch (propertyName)
        {
            case "minimumTextureWeight": prop.floatValue = 0.5f; break;
            case "perlinNoiseScale": prop.floatValue = 0.1f; break;
            case "minimumNoiseAcceptableValue": prop.floatValue = 0.4f; break;
            case "chunkSize": prop.intValue = 64; break;
            case "grassDensity": prop.intValue = 1; break;
            case "leafDispersion": prop.floatValue = 1f; break;
            case "bladeTypeNoiseScale": prop.floatValue = 5f; break;
            case "transitionWidth": prop.floatValue = 0.1f; break;
            case "maxRenderDistance": prop.floatValue = 150f; break;
            case "lod0Distance": prop.floatValue = 30f; break;
            case "lod1Distance": prop.floatValue = 80f; break;
            case "cullPercentage": prop.floatValue = 0.015f; break;
            case "windSpeed": prop.floatValue = 1f; break;
            case "windStrength": prop.floatValue = 0.5f; break;
            case "windDirection": prop.floatValue = 45f; break;
            case "windTurbulence": prop.floatValue = 0.3f; break;
            case "interactionRadius": prop.floatValue = 3f; break;
            case "interactionStrength": prop.floatValue = 1f; break;
            case "maxVerticesPerChunk": prop.intValue = 60000; break;
            case "aoIntensity": prop.floatValue = 0.3f; break;
            case "heightVariationAmount": prop.floatValue = 0.2f; break;
        }
    }

    private void DrawNoiseRangeVisualization()
    {
        EditorGUILayout.LabelField("Distribuição dos Tipos (0.0 - 1.0)", EditorStyles.boldLabel);
        
        Rect rect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
        rect.x += 10;
        rect.width -= 20;
        
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
        
        foreach (var bladeType in _target.bladeTypes)
        {
            if (bladeType == null) continue;
            
            float startX = rect.x + rect.width * bladeType.noiseRangeMin;
            float endX = rect.x + rect.width * bladeType.noiseRangeMax;
            float width = endX - startX;
            
            Rect typeRect = new Rect(startX, rect.y + 5, width, rect.height - 10);
            
            Color typeColor = GetColorForType(bladeType.name);
            EditorGUI.DrawRect(typeRect, typeColor);
            
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            
            if (width > 40)
            {
                GUI.Label(typeRect, bladeType.name, labelStyle);
            }
        }
        
        GUIStyle markStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperCenter
        };
        
        GUI.Label(new Rect(rect.x - 10, rect.y + rect.height, 30, 15), "0.0", markStyle);
        GUI.Label(new Rect(rect.x + rect.width / 2 - 10, rect.y + rect.height, 30, 15), "0.5", markStyle);
        GUI.Label(new Rect(rect.x + rect.width - 10, rect.y + rect.height, 30, 15), "1.0", markStyle);
    }

    private void DrawWindDirectionVisual()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Direção Visual:", EditorStyles.boldLabel);
        
        Rect rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true));
        rect.width = Mathf.Min(rect.width, 100);
        rect.x += (EditorGUIUtility.currentViewWidth - rect.width) / 2 - 20;
        
        Handles.color = new Color(0.3f, 0.3f, 0.3f);
        Handles.DrawSolidDisc(rect.center, Vector3.forward, 45f);
        
        float angleRad = _target.windDirection * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0) * 40f;
        
        Handles.color = new Color(0.3f, 0.8f, 0.3f);
        Handles.DrawLine(rect.center, rect.center + (Vector2)direction);
        Handles.DrawSolidDisc(rect.center + (Vector2)direction, Vector3.forward, 5f);
        
        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(rect.center.x - 25, rect.center.y + 50, 50, 20), 
                  $"{_target.windDirection:F0}°", labelStyle);
    }

    private void AddBladePreset(int presetIndex)
    {
        Undo.RecordObject(_target, "Add Blade Preset");
        
        if (_target.bladeTypes == null)
            _target.bladeTypes = new List<TerrainGrassGenerator.BladeType>();
        
        var newType = new TerrainGrassGenerator.BladeType();
        
        switch (presetIndex)
        {
            case 0: 
                newType.name = "Grama Comum";
                newType.bladeSize = new Vector2(0.05f, 1f);
                newType.noiseRangeMin = 0f;
                newType.noiseRangeMax = 0.4f;
                newType.densityMultiplier = 1.5f;
                newType.segments = new List<TerrainGrassGenerator.BladeSegment>
                {
                    new TerrainGrassGenerator.BladeSegment { supVerticesDistance = 0.03f, heightPercentual = 0.6f }
                };
                break;
                
            case 1: 
                newType.name = "Grama Alta";
                newType.bladeSize = new Vector2(0.04f, 1.5f);
                newType.noiseRangeMin = 0.4f;
                newType.noiseRangeMax = 0.7f;
                newType.densityMultiplier = 1f;
                newType.segments = new List<TerrainGrassGenerator.BladeSegment>
                {
                    new TerrainGrassGenerator.BladeSegment { supVerticesDistance = 0.03f, heightPercentual = 0.5f },
                    new TerrainGrassGenerator.BladeSegment { supVerticesDistance = 0.02f, heightPercentual = 0.3f }
                };
                break;
                
            case 3: 
                newType.name = "Trevo";
                newType.bladeSize = new Vector2(0.08f, 0.5f);
                newType.noiseRangeMin = 0.7f;
                newType.noiseRangeMax = 1f;
                newType.densityMultiplier = 0.8f;
                newType.segments = new List<TerrainGrassGenerator.BladeSegment>
                {
                    new TerrainGrassGenerator.BladeSegment { supVerticesDistance = 0.06f, heightPercentual = 0.8f }
                };
                break;
        }
        
        _target.bladeTypes.Add(newType);
        EditorUtility.SetDirty(_target);
    }

    private Color GetColorForType(string typeName)
    {
        int hash = typeName.GetHashCode();
        Random.InitState(hash);
        
        return new Color(
            Random.Range(0.3f, 0.8f),
            Random.Range(0.5f, 0.9f),
            Random.Range(0.3f, 0.7f),
            0.7f
        );
    }
}