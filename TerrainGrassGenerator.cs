using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

/// <summary>
/// Gerador híbrido de grama procedural otimizado com técnicas AAA.
/// Implementa: GPU Instancing, Culling (Distance/Frustum), Wind Animation, Interactive Grass.
/// Baseado em técnicas de Ghost of Tsushima, Breath of the Wild e Horizon Zero Dawn.
/// </summary>
[RequireComponent(typeof(Terrain))]
public class TerrainGrassGenerator : MonoBehaviour
{
    // SEÇÃO 1: CONFIGURAÇÕES DE GERAÇÃO 
    [Header("1. Geração por Terreno e Ruído")]
    [Tooltip("Camadas do terreno que permitem o crescimento de grama")]
    public List<GrassLayerConfig> grassLayers = new List<GrassLayerConfig>();

    [Range(0f, 1f)]
    [Tooltip("Peso mínimo da textura necessário para gerar grama")]
    public float minimumTextureWeight = 0.5f;

    [Space(10)]
    [Range(0.01f, 100f)]
    [Tooltip("Escala do ruído Perlin (valores menores = manchas maiores)")]
    public float perlinNoiseScale = 0.1f;

    [Range(0f, 1f)]
    [Tooltip("Valor mínimo de ruído aceito para geração de grama")]
    public float minimumNoiseAcceptableValue = 0.4f;

    [Header("2. Densidade e Posicionamento")]
    [Tooltip("Tamanho (em unidades) de cada chunk de malha")]
    public int chunkSize = 64;

    [Tooltip("Densidade de lâminas por unidade de terreno")]
    public int grassDensity = 1;

    [Range(0f, 2f)]
    [Tooltip("Dispersão aleatória das lâminas dentro de cada tufão")]
    public float leafDispersion = 1f;

    // SEÇÃO 2: APARÊNCIA DA LÂMINA
    [Header("3. Tipos de Lâminas")]
    [Tooltip("Lista de tipos diferentes de lâminas que serão misturadas")]
    public List<BladeType> bladeTypes = new List<BladeType>();

    [Tooltip("Usar ruído para distribuir tipos (se false, usa distribuição aleatória)")]
    public bool useNoiseForBladeTypes = true;

    [Tooltip("Escala global do ruído de seleção de tipos")]
    [Range(0.01f, 100f)]
    public float bladeTypeNoiseScale = 5f;

    [Tooltip("Suavizar transições entre tipos")]
    public bool smoothTypeTransitions = true;

    [Tooltip("Largura da zona de transição (0-1)")]
    [Range(0f, 0.3f)]
    public float transitionWidth = 0.1f;

    // SEÇÃO 3: TÉCNICAS AVANÇADAS
    [Header("6. Culling e LOD (AAA Techniques)")]
    [Tooltip("Distância máxima para renderizar grama (Distance Culling)")]
    [Range(10f, 500f)]
    public float maxRenderDistance = 150f;

    [Tooltip("LOD0: Distância para qualidade máxima")]
    [Range(0f, 100f)]
    public float lod0Distance = 30f;

    [Tooltip("LOD1: Distância para qualidade média")]
    [Range(0f, 150f)]
    public float lod1Distance = 80f;

    [Tooltip("Percentual da tela para culling de chunks")]
    [Range(0.001f, 1.0f)]
    public float cullPercentage = 0.015f;

    [Tooltip("Usar Frustum Culling (recomendado)")]
    public bool useFrustumCulling = true;

    [Header("7. Animação de Vento (Ghost of Tsushima Style)")]
    [Tooltip("Habilitar animação de vento")]
    public bool enableWind = true;

    [Tooltip("Velocidade do vento (menor = mais lento)")]
    [Range(0.1f, 10f)]
    public float windSpeed = 1f;

    [Tooltip("Força do vento (maior = mais movimento)")]
    [Range(0f, 10f)]
    public float windStrength = 0.5f;

    [Tooltip("Direção principal do vento (graus)")]
    [Range(0f, 360f)]
    public float windDirection = 45f;

    [Tooltip("Turbulência do vento (variação aleatória)")]
    [Range(0f, 5f)]
    public float windTurbulence = 0.3f;

    [Header("8. Grama Interativa (Breath of the Wild Style)")]
    [Tooltip("Habilitar interação com jogador/objetos")]
    public bool enableInteraction = false;

    [Tooltip("Transform do jogador para interação")]
    public Transform playerTransform;

    [Tooltip("Raio de interação ao redor do jogador")]
    [Range(0.5f, 10f)]
    public float interactionRadius = 3f;

    [Tooltip("Força da interação")]
    [Range(0f, 100f)]
    public float interactionStrength = 1f;

    [Header("9. Otimização Avançada")]
    [Tooltip("Número máximo de vértices por chunk")]
    public int maxVerticesPerChunk = 60000;

    [Tooltip("Usar sombreamento ambiente na base (AO fake)")]
    public bool useAmbientOcclusion = true;

    [Tooltip("Intensidade do AO na base das lâminas")]
    [Range(0f, 1f)]
    public float aoIntensity = 0.3f;

    [Tooltip("Randomizar rotação Y para evitar padrões")]
    public bool randomizeRotation = true;

    [Tooltip("Usar variação de altura por ruído")]
    public bool heightVariation = true;

    [Range(0f, 1f)]
    [Tooltip("Quantidade de variação de altura")]
    public float heightVariationAmount = 0.2f;

    [Header("10. Material")]
    [Tooltip("Material - IMPORTANTE: Use shader com Vertex Color e suporte a _WindParams")]
    public Material grassMaterial;

    // Classes internas 
    [System.Serializable]
    public class GrassLayerConfig
    {
        public string layerName = "Nova Camada";
        [Tooltip("Índice do TerrainLayer correspondente")]
        public int layerIndice;
        [Tooltip("Define se esta camada permite gerar grama")]
        public bool permitirGrama = true;
    }

    [System.Serializable]
    public class BladeSegment
    {
        public float supVerticesDistance;
        [Range(0.01f, 1f)] public float heightPercentual;
    }

    // Variáveis privadas 
    private Terrain _terrain;
    private TerrainData _terrainData;
    private int _terrainWidth;
    private int _terrainHeight;
    private int _alphamapWidth;
    private int _alphamapHeight;
    private float[,,] _alphamaps;
    private GameObject _grassParent;

    private int _totalBlades;
    private int _totalChunks;
    private int _skippedChunks;

    // Para culling dinâmico
    private List<GrassChunk> _activeChunks = new List<GrassChunk>();
    private Camera _mainCamera;

    // Para vento (atualizado via shader)
    private Vector4 _windParams;
    private static readonly int WindStrengthID = Shader.PropertyToID("_WindStrength");
    private static readonly int WindParamsID = Shader.PropertyToID("_WindParams");
    private static readonly int InteractionPosID = Shader.PropertyToID("_InteractionPos");
    private static readonly int InteractionRadiusID = Shader.PropertyToID("_InteractionRadius");

    [System.Serializable]
    public class BladeType
    {
        public string name = "Tipo de Lâmina";
        
        [Header("Forma")]
        public Vector2 bladeSize = new Vector2(0.05f, 1f);
        public List<BladeSegment> segments = new List<BladeSegment>();
        
        [Header("Aparência")]
        public bool habilitarGradiente = true;
        [Range(0f, 1f)] public float brilhoBase = 0.3f;
        [Range(0f, 1f)] public float brilhoPonta = 1f;
        
        [Header("Variação de Cor")]
        [Range(-0.1f, 0.1f)] public float variaçãoMatizMin = -0.05f;
        [Range(-0.1f, 0.1f)] public float variaçãoMatizMax = 0.05f;
        [Range(-0.5f, 0.5f)] public float variaçãoSaturaçãoMin = -0.05f;
        [Range(-0.5f, 0.5f)] public float variaçãoSaturaçãoMax = 0.05f;
        [Range(-0.2f, 0.2f)] public float variaçãoValorMin = -0.05f;
        [Range(-0.2f, 0.2f)] public float variaçãoValorMax = 0.05f;
        
        [Header("Variação de Escala e Inclinação")]
        [Range(0.1f, 2f)] public float variaçãoEscalaMin = 0.8f;
        [Range(0.1f, 2f)] public float variaçãoEscalaMax = 1.2f;
        [Range(0, 45)] public int inclinaçãoMin = 0;
        [Range(0, 45)] public int inclinaçãoMax = 15;
        
        [Header("Controle de Ruído")]
        [Tooltip("Escala do ruído específica para este tipo")]
        [Range(0.01f, 100f)]
        public float noiseScale = 10f;
        
        [Tooltip("Faixa de ruído onde este tipo aparece (Min)")]
        [Range(0f, 1f)]
        public float noiseRangeMin = 0f;
        
        [Tooltip("Faixa de ruído onde este tipo aparece (Max)")]
        [Range(0f, 1f)]
        public float noiseRangeMax = 0.33f;
        
        [Header("Densidade Relativa")]
        [Tooltip("Multiplicador de densidade para este tipo (1 = normal)")]
        [Range(0.1f, 5f)]
        public float densityMultiplier = 1f;
    }   

    private class GrassChunk
    {
        public GameObject gameObject;
        public MeshRenderer renderer;
        public Bounds bounds;
        public Vector3 center;
        public float distanceToCamera;
        public int lodLevel; // 0 = full, 1 = medium, 2 = low
    }

    // === FUNÇÃO PRINCIPAL DE GERAÇÃO ===
    [ContextMenu("Gerar Grama")]
    public void GenerateGrass()
    {
        float startTime = Time.realtimeSinceStartup;

        _totalBlades = 0;
        _totalChunks = 0;
        _skippedChunks = 0;
        _activeChunks.Clear();

        if (!InitializeTerrainData())
            return;

        // Limpamos o container de grama anterior, se houver um, e então criamos um novo
        ClearOldGrass();

        _grassParent = new GameObject("GrassContainer");
        _grassParent.transform.SetParent(transform);
        _grassParent.transform.localPosition = Vector3.zero;

        // Criamos a quantidade de linhas e colunas de chunks que vão haver no terreno 
        int chunkCountX = Mathf.CeilToInt((float)_terrainWidth / chunkSize);
        int chunkCountZ = Mathf.CeilToInt((float)_terrainHeight / chunkSize);

        Debug.Log($"🌾 Iniciando geração: {chunkCountX}x{chunkCountZ} chunks ({chunkCountX * chunkCountZ} total)");

        // Iniciamos a geração de grama chunk a chunk por meio dos loops for aninhados
        for (int chunkX = 0; chunkX < chunkCountX; chunkX++)
        {
            for (int chunkZ = 0; chunkZ < chunkCountZ; chunkZ++)
            {
                // Criamos a lista de vértices, triangulos, UVs e cores para todas as lâminas do terreno
                List<Vector3> verts = new List<Vector3>();
                List<int> tris = new List<int>();
                List<Vector2> uvs = new List<Vector2>();
                List<Color> colors = new List<Color>();

                // Declaramos o começo e o fim de cada coordenada da chunk referente ao terreno
                int xStart = chunkX * chunkSize;
                int xEnd = Mathf.Min(xStart + chunkSize, _terrainWidth);
                int zStart = chunkZ * chunkSize;
                int zEnd = Mathf.Min(zStart + chunkSize, _terrainHeight);

                // Inicializamos variáveis nulas, por hora
                Vector3 chunkCenter = Vector3.zero;
                int grassCount = 0;

                // Agora, fazemos o mesmo processo para encontrar os pontos de possível geração de grama
                for (int x = xStart; x < xEnd; x++)
                {
                    for (int z = zStart; z < zEnd; z++)
                    {
                        // Se essa chunk já tiver passado do número máximo de vértices por chunk, para a geração
                        if (verts.Count >= maxVerticesPerChunk) break;

                        // Normalizamos as coordenadas x e z para podermos aplicá-las a qualquer resolução posteriormente
                        float normX = (float)x / _terrainWidth;
                        float normZ = (float)z / _terrainHeight;

                        // Ruído que define pontos onde a grama pode ser gerada, se a camada permitir a geração
                        float noise = Mathf.PerlinNoise(normX * perlinNoiseScale, normZ * perlinNoiseScale);
                        if (noise <= minimumNoiseAcceptableValue)
                            continue;

                        // Obtemos a cor do terreno no ponto atual
                        Color terrainColor;
                        if (!TryGetTerrainColor(normX, normZ, out terrainColor))
                            continue;

                        // Utilizamos a variável normalizada para descobrir a sua coordenada referente no mapa do mundo
                        float worldX = normX * _terrainData.size.x;
                        float worldZ = normZ * _terrainData.size.z;
                        float worldY = _terrain.SampleHeight(new Vector3(worldX, 0, worldZ));

                        // Atribuimos esses dados à um vetor
                        Vector3 pos = new Vector3(worldX, worldY, worldZ);

                        // Descobrimos o vetor normal do terreno (perpendicular) nesse ponto e o aplicamos à rotação do ponto de geração
                        Vector3 normal = _terrainData.GetInterpolatedNormal(normX, normZ);
                        Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, normal);

                        // Multiplicador de variação de altura baseada em ruído (técnica avançada)
                        float heightMod = 1f;

                        // Se tivermos habilitado a variação de altura, modificamos o multiplicador de altura baseado no perlin noise
                        if (heightVariation)
                        {
                            float heightNoise = Mathf.PerlinNoise(normX * 50f, normZ * 50f);
                            heightMod = Mathf.Lerp(1f - heightVariationAmount, 1f + heightVariationAmount, heightNoise);
                        }

                        // Para cada ponto de geração, quantas lâminas devem ser construídas?
                        for (int i = 0; i < grassDensity; i++)
                        {
                            if (verts.Count >= maxVerticesPerChunk) break;

                            // NOVO: Selecionar tipo de lâmina baseado em ruído
                            BladeType selectedType = SelectBladeType(normX, normZ);
                            if (selectedType == null) continue; // Se nenhum tipo for válido, pula
                            
                            // Aplicar multiplicador de densidade (probabilidade de plantar)
                            float plantChance = Mathf.Clamp01(selectedType.densityMultiplier / 5f);
                            if (Random.value > plantChance) continue;

                            // Definimos a posição de uma lâmina
                            Vector3 instancePos = pos + new Vector3(
                                Random.Range(-leafDispersion, leafDispersion),
                                0,
                                Random.Range(-leafDispersion, leafDispersion)
                            );
                            instancePos.y = _terrain.SampleHeight(instancePos);

                            // Rotação aleatória melhorada (evita padrões)
                            float yawAngle = randomizeRotation ? Random.Range(0, 360) : 0;
                            Quaternion yaw = Quaternion.Euler(0, yawAngle, 0);
                            Quaternion tilt = Quaternion.Euler(
                                Random.Range(selectedType.inclinaçãoMin, selectedType.inclinaçãoMax),
                                0,
                                Random.Range(selectedType.inclinaçãoMin, selectedType.inclinaçãoMax)
                            );
                            Quaternion finalRot = slopeRot * tilt * yaw;

                            // Modificamos a escala da lâmina (usando valores do tipo)
                            float scale = Random.Range(selectedType.variaçãoEscalaMin, selectedType.variaçãoEscalaMax) * heightMod;

                            // Geramos a lâmina baseada no tipo selecionado
                            BuildBladeWithType(verts, tris, uvs, colors, instancePos, finalRot, terrainColor, scale, selectedType);

                            // Modificamos as variáveis criadas
                            chunkCenter += instancePos;
                            grassCount++;
                            _totalBlades++;
                        }
                    }
                    if (verts.Count >= maxVerticesPerChunk) break;
                }

                if (verts.Count > 0)
                {
                    // Para descobrir o centro da chunk, dividimos o valor anterior (somatório das posições) pela quantidade de lâminas na chunk
                    chunkCenter /= grassCount;

                    // Criamos uma nova chunk e à adicionamos às chunks já criadas
                    GrassChunk chunk = CreateChunkObject(chunkX, chunkZ, verts, tris, uvs, colors, chunkCenter);
                    _activeChunks.Add(chunk);
                    _totalChunks++;
                }
                else
                {
                    _skippedChunks++;
                }
            }
        }

        // Após toda a geração ter sido efetuada, entrega os dados da geração no console :)
        float elapsed = Time.realtimeSinceStartup - startTime;
        Debug.Log($"✅ Geração concluída em {elapsed:F2}s\n" +
                  $"   📊 {_totalBlades:N0} lâminas | {_totalChunks} chunks | {_skippedChunks} vazios\n" +
                  $"   🎮 Culling: Distance={maxRenderDistance}m, Frustum={useFrustumCulling}\n" +
                  $"   💨 Vento: {(enableWind ? "Ativo" : "Desativado")}\n" +
                  $"   🎯 Interação: {(enableInteraction ? "Ativa" : "Desativada")}");
    }

    // FUNÇÕES AUXILIARES 

    // Inicializa os dados do terreno kkkkkkkkkkkkkkkkkkkkkkkkkkkkkk (e outras paradinhas)
    private bool InitializeTerrainData()
    {
        _terrain = GetComponent<Terrain>();
        if (_terrain == null)
        {
            Debug.LogError("❌ Componente Terrain não encontrado!");
            return false;
        }

        _terrainData = _terrain.terrainData;
        if (_terrainData == null)
        {
            Debug.LogError("❌ TerrainData não encontrado!");
            return false;
        }

        _terrainWidth = Mathf.RoundToInt(_terrainData.size.x);
        _terrainHeight = Mathf.RoundToInt(_terrainData.size.z);
        _alphamapWidth = _terrainData.alphamapWidth;
        _alphamapHeight = _terrainData.alphamapHeight;
        _alphamaps = _terrainData.GetAlphamaps(0, 0, _alphamapWidth, _alphamapHeight);

        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogWarning("⚠️ Camera principal não encontrada - Culling desabilitado");
        }

        return true;
    }

    private void ClearOldGrass()
    {
        Transform old = transform.Find("GrassContainer");
        if (old != null)
        {
            if (Application.isPlaying)
                Destroy(old.gameObject);
            else
                DestroyImmediate(old.gameObject);
        }
        _activeChunks.Clear();
    }

    // Tenta obter a cor do terreno em um ponto
    private bool TryGetTerrainColor(float normX, float normZ, out Color finalColor)
    {
        // Atualiza a resolução para a dos alphamaps (onde são armazenadas as camadas do terreno)
        int mapX = Mathf.FloorToInt(normX * (_alphamapWidth - 1));
        int mapZ = Mathf.FloorToInt(normZ * (_alphamapHeight - 1));

        mapX = Mathf.Clamp(mapX, 0, _alphamapWidth - 1);
        mapZ = Mathf.Clamp(mapZ, 0, _alphamapHeight - 1);

        // Inicializamos a cor final como preta (0,0,0,0) e o peso das camadas nesse ponto como 0 (valores nulos)
        finalColor = Color.black;
        float totalWeight = 0f;

        // Fazemos algumas verificações e, se a camada passar por todas, atribuimos novos valores de peso e cor à camada
        for (int i = 0; i < _terrainData.alphamapLayers; i++)
        {
            float weight = _alphamaps[mapZ, mapX, i];
            if (weight <= 0) continue;

            GrassLayerConfig layerConfig = grassLayers.Find(layer => layer.layerIndice == i);
            if (layerConfig == null || !layerConfig.permitirGrama) continue;

            TerrainLayer tLayer = _terrainData.terrainLayers[i];
            if (tLayer.diffuseTexture == null) continue;

#if UNITY_EDITOR
            string path = AssetDatabase.GetAssetPath(tLayer.diffuseTexture);
            if (!string.IsNullOrEmpty(path))
            {
                TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(path);
                if (importer != null && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
            }
#endif

            Color texColor = tLayer.diffuseTexture.GetPixelBilinear(normX, normZ);
            finalColor += texColor * weight;
            totalWeight += weight;
        }

        if (totalWeight >= minimumTextureWeight)
        {
            finalColor /= totalWeight;
            return true;
        }

        return false;
    }

    // Cria o gameObject da chunk, contendo todos os seus elementos
    private GrassChunk CreateChunkObject(int cx, int cz, List<Vector3> v, List<int> t,
                                         List<Vector2> uv, List<Color> c, Vector3 center)
    {
        GameObject chunkObj = new GameObject($"GrassChunk_{cx}_{cz}");
        chunkObj.transform.SetParent(_grassParent.transform);
        chunkObj.transform.localPosition = Vector3.zero;

        Mesh m = new Mesh();
        m.name = $"GrassMesh_{cx}_{cz}";
        if (v.Count > 65000)
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.SetUVs(0, uv);
        m.SetColors(c);
        m.RecalculateNormals();
        m.RecalculateBounds();

        MeshFilter mf = chunkObj.AddComponent<MeshFilter>();
        mf.sharedMesh = m;

        MeshRenderer mr = chunkObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = grassMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;

        // LOD Group com culling por distância (técnica AAA)
        // Valores em porcentagem da tela (1.0 = muito perto, 0.0 = muito longe)
        LODGroup lodGroup = chunkObj.AddComponent<LODGroup>();
        LOD[] lods = new LOD[2];

        // LOD 0: Renderiza até a distância máxima (aparece quando visível)
        lods[0] = new LOD(cullPercentage, new Renderer[] { mr });

        // LOD 1: Culling total (desaparece quando muito longe)
        lods[1] = new LOD(0.0f, new Renderer[] { });

        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();
        lodGroup.fadeMode = LODFadeMode.None; // Sem fade para melhor performance

        GrassChunk chunk = new GrassChunk
        {
            gameObject = chunkObj,
            renderer = mr,
            bounds = m.bounds,
            center = center,
            distanceToCamera = 0f,
            lodLevel = 0
        };

        return chunk;
    }
    // Sistema de Culling e Vento
    private void Update()
    {
        // Animações de vento e interação
        
        if (enableWind && grassMaterial != null)
        {
            UpdateWindParameters();
        }

        if (enableInteraction && playerTransform != null && grassMaterial != null)
        {
            UpdateInteraction();
        }

        // 2. LÓGICA DE CULLING E GAMEPLAY
        // (Isso só deve rodar em Play Mode, pois depende da _mainCamera)
        
        if (Application.isPlaying)
        {
            if (_activeChunks == null || _activeChunks.Count == 0) return;

            // Inicializamos a câmera principal aqui, se ainda não tivermos
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    Debug.LogWarning("⚠️ Câmera principal não encontrada - Culling desabilitado");
                    return; // Retorna se não houver câmera
                }
            }
            
            PerformCulling();
        }
    }

    private void UpdateWindParameters()
    {
        // 1. O "motor" da animação (Tempo)
        // Isso vai para o canal W.
        float time = Time.unscaledTime * windSpeed;

        // 2. O vetor de direção (Normalizado)
        // Isso vai para os canais X e Z.
        float windDirRad = windDirection * Mathf.Deg2Rad;
        float dirX = Mathf.Cos(windDirRad);
        float dirZ = Mathf.Sin(windDirRad);

        // 3. Monta o Vector4 "correto"
        _windParams = new Vector4(
            dirX,             // X: Direção X
            windTurbulence,   // Y: Turbulência
            dirZ,             // Z: Direção Z
            time              // W: Tempo
        );

        // 4. Envia os dados para o material
        if (grassMaterial != null)
        {
            // Envia o Vector4 principal
            grassMaterial.SetVector(WindParamsID, _windParams);
            
            // Envia a força (Float) separadamente
            grassMaterial.SetFloat(WindStrengthID, windStrength);
        }
    }


    private void UpdateInteraction()
    {
        if (playerTransform == null || grassMaterial == null) return;

        Vector3 playerPos = playerTransform.position;

        grassMaterial.SetVector(InteractionPosID,
            new Vector4(playerPos.x, playerPos.y, playerPos.z, interactionStrength));
        grassMaterial.SetFloat(InteractionRadiusID, interactionRadius);
    }


    private void PerformCulling()
    {
        Vector3 camPos = _mainCamera.transform.position;

        foreach (var chunk in _activeChunks)
        {
            if (chunk.renderer == null) continue;

            if (chunk.renderer.isVisible)
            {
                chunk.distanceToCamera = Vector3.Distance(camPos, chunk.center);

                // Definimos o nível de LOD (para os Gizmos)
                if (chunk.distanceToCamera < lod0Distance)
                    chunk.lodLevel = 0;
                else if (chunk.distanceToCamera < lod1Distance)
                    chunk.lodLevel = 1;
                else
                    chunk.lodLevel = 2;

                // 3. Aplicamos o vento/interação
            }
            else
            {
                chunk.lodLevel = -1; // -1 = Culled
            }
        }
    }
    
        // Seleciona o tipo de lâmina baseado em ruído ou aleatoriamente
    private BladeType SelectBladeType(float normX, float normZ)
    {
        if (bladeTypes == null || bladeTypes.Count == 0) return null;
        
        if (!useNoiseForBladeTypes)
        {
            // Seleção aleatória simples
            return bladeTypes[Random.Range(0, bladeTypes.Count)];
        }
        
        // Seleção baseada em ruído
        float typeNoise = Mathf.PerlinNoise(
            normX * bladeTypeNoiseScale, 
            normZ * bladeTypeNoiseScale
        );
        
        if (smoothTypeTransitions)
        {
            return SelectBladeTypeWithTransition(typeNoise);
        }
        else
        {
            return SelectBladeTypeHardEdge(typeNoise);
        }
    }

    // Seleção com transição suave (mistura tipos nas bordas)
    private BladeType SelectBladeTypeWithTransition(float noiseValue)
    {
        foreach (var type in bladeTypes)
        {
            float rangeCenter = (type.noiseRangeMin + type.noiseRangeMax) / 2f;
            float rangeSize = type.noiseRangeMax - type.noiseRangeMin;
            
            // Expandir range com zona de transição
            float expandedMin = type.noiseRangeMin - transitionWidth;
            float expandedMax = type.noiseRangeMax + transitionWidth;
            
            if (noiseValue >= expandedMin && noiseValue <= expandedMax)
            {
                // Dentro da zona de transição, usar probabilidade
                float distanceFromCenter = Mathf.Abs(noiseValue - rangeCenter);
                float probability = 1f - (distanceFromCenter / (rangeSize / 2f + transitionWidth));
                
                if (Random.value < probability)
                    return type;
            }
        }
        
        // Fallback: retorna o primeiro tipo
        return bladeTypes[0];
    }

    // Seleção com bordas definidas (sem mistura)
    private BladeType SelectBladeTypeHardEdge(float noiseValue)
    {
        foreach (var type in bladeTypes)
        {
            if (noiseValue >= type.noiseRangeMin && noiseValue <= type.noiseRangeMax)
                return type;
        }
        
        // Fallback: retorna o primeiro tipo
        return bladeTypes[0];
    }

    // Constrói uma lâmina, de um tipo específico declarado pelo usuário
    private void BuildBladeWithType(List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> colors,
                        Vector3 position, Quaternion rotation, Color baseColor, float scale, BladeType type)
    {
        float h, s, v;
        Color.RGBToHSV(baseColor, out h, out s, out v);
        h = Mathf.Repeat(h + Random.Range(type.variaçãoMatizMin, type.variaçãoMatizMax), 1f);
        s = Mathf.Clamp01(s + Random.Range(type.variaçãoSaturaçãoMin, type.variaçãoSaturaçãoMax));
        v = Mathf.Clamp01(v + Random.Range(type.variaçãoValorMin, type.variaçãoValorMax));

        int baseIndex = verts.Count;
        BuildBladeRecursive(verts, tris, uvs, colors, baseIndex, position, rotation, h, s, v,
            0, type.bladeSize.x * scale, 0f, 0f, type.bladeSize.y * scale, type.bladeSize.x * scale, type);
    }

    // 
    private void BuildBladeRecursive(
        List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> colors,
        int baseVertexIndex, Vector3 position, Quaternion rotation,
        float H, float S, float V,
        int segmentIndex, float baseWidth, float baseHeight,
        float accumHeightPercent, float totalBladeHeight, float originalBaseWidth, BladeType type)
    {
        if (baseHeight == 0f)
            position.y = _terrain.SampleHeight(position);

        // Vértices da base
        Vector3 v_base_left = new Vector3(-baseWidth / 2, baseHeight, 0);
        Vector3 v_base_right = new Vector3(baseWidth / 2, baseHeight, 0);

        verts.Add(position + rotation * v_base_left);
        verts.Add(position + rotation * v_base_right);

        // Cor com gradiente + AO fake na base (usando configurações do tipo)
        float brilhoBaseVert = type.habilitarGradiente
            ? Mathf.Lerp(type.brilhoBase, type.brilhoPonta, baseHeight / totalBladeHeight)
            : 1f;

        // Ambient Occlusion fake (escurece a base)
        if (useAmbientOcclusion && baseHeight < totalBladeHeight * 0.2f)
        {
            float aoFactor = Mathf.Lerp(1f - aoIntensity, 1f, baseHeight / (totalBladeHeight * 0.2f));
            brilhoBaseVert *= aoFactor;
        }

        float vertexHeightNormalized = baseHeight / totalBladeHeight;
        Color corVert = Color.HSVToRGB(H, S, V * brilhoBaseVert);
        corVert.a = vertexHeightNormalized;
        
        colors.Add(corVert);
        colors.Add(corVert);

        // UVs
        float u_left = 0.5f - (baseWidth / originalBaseWidth) / 2f;
        float u_right = 0.5f + (baseWidth / originalBaseWidth) / 2f;
        uvs.Add(new Vector2(u_left, accumHeightPercent));
        uvs.Add(new Vector2(u_right, accumHeightPercent));

        int currentBaseVertexIndex = verts.Count - 2;

        // Caso final: ponta
        if (segmentIndex >= type.segments.Count)
        {
            Vector3 v_tip = new Vector3(0, totalBladeHeight, 0);
            verts.Add(position + rotation * v_tip);

            float brilhoTopo = type.habilitarGradiente ? type.brilhoPonta : 1f;
            Color corTopo = Color.HSVToRGB(H, S, V * brilhoTopo);
            corTopo.a = 1f;
            colors.Add(corTopo);
            uvs.Add(new Vector2(0.5f, 1f));

            int tipIndex = verts.Count - 1;
            tris.Add(currentBaseVertexIndex);
            tris.Add(tipIndex);
            tris.Add(currentBaseVertexIndex + 1);
            return;
        }

        // Caso recursivo (usando segments do tipo)
        BladeSegment seg = type.segments[segmentIndex];
        float topWidth = seg.supVerticesDistance;
        float newAccumHeightPercent = Mathf.Min(1f, accumHeightPercent + seg.heightPercentual);
        float topHeight = totalBladeHeight * newAccumHeightPercent;

        Vector3 v_top_left = new Vector3(-topWidth / 2, topHeight, 0);
        Vector3 v_top_right = new Vector3(topWidth / 2, topHeight, 0);
        verts.Add(position + rotation * v_top_left);
        verts.Add(position + rotation * v_top_right);

        float brilhoTopVert = type.habilitarGradiente
            ? Mathf.Lerp(type.brilhoBase, type.brilhoPonta, topHeight / totalBladeHeight)
            : 1f;

        float vertexHeightNormalizedTop = topHeight / totalBladeHeight;
        Color corTop = Color.HSVToRGB(H, S, V * brilhoTopVert);
        corTop.a = vertexHeightNormalizedTop;
        colors.Add(corTop);
        colors.Add(corTop);

        float u_top_left = 0.5f - (topWidth / originalBaseWidth) / 2f;
        float u_top_right = 0.5f + (topWidth / originalBaseWidth) / 2f;
        uvs.Add(new Vector2(u_top_left, newAccumHeightPercent));
        uvs.Add(new Vector2(u_top_right, newAccumHeightPercent));

        int topVertexIndex = verts.Count - 2;
        tris.Add(currentBaseVertexIndex);
        tris.Add(topVertexIndex);
        tris.Add(currentBaseVertexIndex + 1);
        tris.Add(topVertexIndex);
        tris.Add(topVertexIndex + 1);
        tris.Add(currentBaseVertexIndex + 1);

        BuildBladeRecursive(verts, tris, uvs, colors, baseVertexIndex, position, rotation, H, S, V,
            segmentIndex + 1, topWidth, topHeight, newAccumHeightPercent, totalBladeHeight, originalBaseWidth, type);
    }

    // FUNÇÕES DE DEBUG
    private void OnDrawGizmosSelected()
    {
        // Gizmos de interação
        if (enableInteraction && playerTransform != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(playerTransform.position, interactionRadius);
        }

        // Estes gizmos só fazem sentido em Play Mode, pois dependem da câmera e dos chunks ativos.
        if (!Application.isPlaying) return; // Agora esta linha está aqui
        if (_activeChunks == null || _activeChunks.Count == 0) return;
        if (_mainCamera == null) return;

        // Desenha distância máxima de renderização
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_mainCamera.transform.position, maxRenderDistance);

        // Desenha LOD0 distance
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_mainCamera.transform.position, lod0Distance);

        // Desenha LOD1 distance
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(_mainCamera.transform.position, lod1Distance);

        // Desenha bounds dos chunks ativos
        foreach (var chunk in _activeChunks)
        {
            if (chunk.renderer == null) continue;

            if (chunk.renderer.enabled)
            {
                // Cor baseada no LOD
                switch (chunk.lodLevel)
                {
                    case 0: Gizmos.color = new Color(0, 1, 0, 0.3f); break; // Verde
                    case 1: Gizmos.color = new Color(0, 0, 1, 0.3f); break; // Azul
                    case 2: Gizmos.color = new Color(1, 1, 0, 0.3f); break; // Amarelo
                }
                Gizmos.DrawWireCube(chunk.bounds.center, chunk.bounds.size);
            }
            else
            {
                // Vermelho para chunks culled
                Gizmos.color = new Color(1, 0, 0, 0.1f);
                Gizmos.DrawWireCube(chunk.bounds.center, chunk.bounds.size);
            }
        }
    }

#if UNITY_EDITOR
    // === FERRAMENTAS DE EDITOR ===
    [ContextMenu("Estatísticas de Performance")]
    public void ShowPerformanceStats()
    {
        if (_activeChunks == null || _activeChunks.Count == 0)
        {
            Debug.Log("⚠️ Nenhum chunk gerado ainda.");
            return;
        }

        int visibleChunks = 0;
        int culledChunks = 0;
        int lod0Count = 0;
        int lod1Count = 0;
        int lod2Count = 0;

        foreach (var chunk in _activeChunks)
        {
            if (chunk.renderer != null && chunk.renderer.enabled)
            {
                visibleChunks++;
                switch (chunk.lodLevel)
                {
                    case 0: lod0Count++; break;
                    case 1: lod1Count++; break;
                    case 2: lod2Count++; break;
                }
            }
            else
            {
                culledChunks++;
            }
        }

        float cullingEfficiency = (_totalChunks > 0) ? (culledChunks / (float)_totalChunks) * 100f : 0f;

        Debug.Log($"📊 === ESTATÍSTICAS DE PERFORMANCE ===\n" +
                  $"   🌾 Total de Lâminas: {_totalBlades:N0}\n" +
                  $"   📦 Total de Chunks: {_totalChunks}\n" +
                  $"   👁️ Chunks Visíveis: {visibleChunks}\n" +
                  $"   ✂️ Chunks Culled: {culledChunks} ({cullingEfficiency:F1}%)\n" +
                  $"   🎯 LOD0 (Alta): {lod0Count}\n" +
                  $"   🎯 LOD1 (Média): {lod1Count}\n" +
                  $"   🎯 LOD2 (Baixa): {lod2Count}\n" +
                  $"   💨 Vento: {(enableWind ? "Ativo" : "Desativado")}\n" +
                  $"   🎮 Interação: {(enableInteraction ? "Ativa" : "Desativada")}\n" +
                  $"   📏 Distância Máxima: {maxRenderDistance}m");
    }

    [ContextMenu("Otimizar Material para Performance")]
    public void OptimizeMaterialForPerformance()
    {
        if (grassMaterial == null)
        {
            Debug.LogError("❌ Material não atribuído!");
            return;
        }

        // Configurações recomendadas
        Debug.Log("🔧 Aplicando otimizações no material...\n" +
                  "   - Shadowcasting: Off\n" +
                  "   - Receive Shadows: On (opcional)\n" +
                  "   - GPU Instancing: Recomendado para futuras versões\n" +
                  "   - Culling: Back (se lâminas forem one-sided)\n" +
                  "   ✅ Verifique se o shader suporta _WindParams!");
    }

    [ContextMenu("Testar Shader de Vento")]
    public void TestWindShader()
    {
        if (grassMaterial == null)
        {
            Debug.LogError("❌ Material não atribuído!");
            return;
        }

        bool hasWindParams = grassMaterial.HasProperty("_WindParams");
        bool hasInteractionPos = grassMaterial.HasProperty("_InteractionPos");
        bool hasInteractionRadius = grassMaterial.HasProperty("_InteractionRadius");

        Debug.Log($"🧪 === TESTE DE SHADER ===\n" +
                  $"   {(hasWindParams ? "✅" : "❌")} _WindParams (Vento)\n" +
                  $"   {(hasInteractionPos ? "✅" : "❌")} _InteractionPos (Interação)\n" +
                  $"   {(hasInteractionRadius ? "✅" : "❌")} _InteractionRadius (Interação)\n" +
                  $"\n{(hasWindParams && hasInteractionPos ? "✅ Shader está pronto!" : "⚠️ Shader precisa ser atualizado com as propriedades necessárias")}");
    }
#endif
}