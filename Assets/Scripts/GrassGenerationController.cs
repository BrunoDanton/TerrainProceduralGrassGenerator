using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Orquestrador principal responsável por ler configurações do Inspector e gerenciar o ciclo de vida.
/// </summary>
[RequireComponent(typeof(Terrain))]
public class GrassGenerationController : MonoBehaviour
{

    [Header("Configurações Base")]
    [SerializeField] private List<GrassLayerConfig> _grassLayersList = new List<GrassLayerConfig>();
    [SerializeField] private List<BladeType> _bladeTypesList = new List<BladeType>();
    
    [SerializeField] private int _chunkSize = 64;
    [SerializeField] private int _grassDensity = 1;
    [SerializeField] private float _leafDispersion = 1f;
    [SerializeField] private int _maxVerticesPerChunk = 60000;

    [Header("Culling e Performance")]
    [SerializeField] private float _maxRenderDistance = 150f;
    [SerializeField] private bool _shouldUseFrustumCulling = true;

    [Header("Vento e Interação")]
    [SerializeField] private bool _isWindEnabled = true;
    [SerializeField] private bool _canInteract = false;
    [SerializeField] private Transform _playerTransform;

    [Header("Materiais")]
    [SerializeField] private Material _grassMaterial;
    [SerializeField] private Material _interactionFadeMaterial;

    [Header("Configurações de Vento")]
    [SerializeField] private float _windSpeed = 1f;
    [SerializeField] private float _windStrength = 0.5f;
    [SerializeField] private float _windDirection = 45f;
    [SerializeField] private float _windTurbulence = 0.3f;

    [Header("Configurações de Interação (Fallback)")]
    [SerializeField] private float _interactionRadius = 3f;
    [SerializeField] private float _interactionStrength = 1f;

    [Header("Distribuição e Ruído")]
    [SerializeField] private float _perlinNoiseScale = 0.1f;
    [SerializeField] private float _clumpingScale = 10f;
    [Range(0f, 1f)] [SerializeField] private float _minimumAcceptableNoise = 0.4f;

    [Header("Configurações Avançadas")]
    [Range(0f, 1f)] [SerializeField] private float _terrainNormalBlend = 0.7f;
    [SerializeField] private float _minScaleMultiplier = 0.8f;
    [SerializeField] private float _maxScaleMultiplier = 1.3f;
    [SerializeField] private float _frustumPadding = 2f;

    // Cache dos IDs do Shader para máxima performance
    private static readonly int _windStrengthId = Shader.PropertyToID("_WindStrength");
    private static readonly int _windParamsId = Shader.PropertyToID("_WindParams");
    private static readonly int _interactionPosId = Shader.PropertyToID("_InteractionPos");
    private static readonly int _interactionRadiusId = Shader.PropertyToID("_InteractionRadius");

    // Variáveis Privadas
    private Terrain _terrainInstance;
    private Camera _mainCamera;
    private Plane[] _cameraFrustumPlanesArray;
    private List<GrassChunk> _activeChunksList = new List<GrassChunk>();
    private Vector4 _currentWindParams;
    private RenderTexture _interactionMap1;
    private RenderTexture _interactionMap2;
    private bool _isFirstMapActive = true;
    
    private int _totalBladesAmount;
    private int _totalChunksAmount;

    private void Start()
    {
        InitializeSystem();
    }

    private void Update()
    {
        if (_isWindEnabled)
        {
            UpdateWindSystem();
        }

        if (_canInteract)
        {
            UpdateInteractionSystem();
        }

        ProcessCulling();
    }

    /// <summary>
    /// Inicializa as referências básicas necessárias para o controle.
    /// </summary>
    private void InitializeSystem()
    {
        _terrainInstance = GetComponent<Terrain>();
        _mainCamera = Camera.main;

        if (_mainCamera == null)
        {
            Debug.LogWarning("GrassGenerationController: Camera principal não encontrada.");
        }

        if (_terrainInstance != null && _canInteract)
        {
            int resolution = _terrainInstance.terrainData.heightmapResolution;
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.ARGBFloat);
            descriptor.enableRandomWrite = true;
            
            if (_interactionMap1 == null)
            {
                _interactionMap1 = new RenderTexture(descriptor);
                _interactionMap1.Create();
            }
            if (_interactionMap2 == null)
            {
                _interactionMap2 = new RenderTexture(descriptor);
                _interactionMap2.Create();
            }
        }
    }
    
    /// <summary>
    /// Inicia o processo de construção procedural.
    /// </summary>
    [ContextMenu("Gerar Grama")]
    public void GenerateGrass()
    {
        InitializeSystem();
        ClearOldGrass();

        TerrainDataProcessor terrainProcessor = new TerrainDataProcessor();
        if (!terrainProcessor.TryInitialize(_terrainInstance)) return;

        GameObject containerObject = new GameObject("GrassContainer");
        containerObject.transform.SetParent(transform);
        containerObject.transform.localPosition = Vector3.zero;

        GrassMeshBuilder meshBuilder = new GrassMeshBuilder(terrainProcessor, containerObject.transform, _grassMaterial);
        
        _activeChunksList = meshBuilder.BuildGrassChunksList(
            _chunkSize, _grassDensity, _leafDispersion, _grassLayersList, _bladeTypesList, 0.5f,
            _perlinNoiseScale, _clumpingScale, _minimumAcceptableNoise, _maxVerticesPerChunk,
            _terrainNormalBlend, _minScaleMultiplier, _maxScaleMultiplier
        );

        Debug.Log($"Geração concluída! Total de Chunks: {_activeChunksList.Count}");
    }

    /// <summary>
    /// Executa a lógica de otimização de Frustum e Distance Culling aplicando margem de segurança.
    /// </summary>
    private void ProcessCulling()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;
        
        if (_activeChunksList.Count == 0 || _mainCamera == null) return;

        Vector3 cameraPosition = _mainCamera.transform.position;

        if (_shouldUseFrustumCulling)
        {
            _cameraFrustumPlanesArray = GeometryUtility.CalculateFrustumPlanes(_mainCamera);
        }

        foreach (var chunk in _activeChunksList)
        {
            if (chunk.ChunkRenderer == null) continue;

            chunk.DistanceToCamera = Vector3.Distance(cameraPosition, chunk.CenterPosition);
            bool isVisible = true;

            if (chunk.DistanceToCamera > _maxRenderDistance)
            {
                isVisible = false;
            }
            else if (_shouldUseFrustumCulling)
            {
                // Expande temporariamente os limites do chunk para evitar que a grama suma abruptamente nas bordas
                Bounds expandedBounds = chunk.ChunkBounds;
                expandedBounds.Expand(_frustumPadding);
                isVisible = GeometryUtility.TestPlanesAABB(_cameraFrustumPlanesArray, expandedBounds);
            }

            if (chunk.ChunkRenderer.enabled != isVisible)
            {
                chunk.ChunkRenderer.enabled = isVisible;
            }
        }
    }

    /// <summary>
    /// Atualiza os vetores de vento no Shader.
    /// </summary>
    private void UpdateWindSystem()
    {
        if (_grassMaterial == null) return;

        float scaledTime = Time.unscaledTime * _windSpeed;
        float windDirectionRadians = _windDirection * Mathf.Deg2Rad;
        float directionX = Mathf.Cos(windDirectionRadians);
        float directionZ = Mathf.Sin(windDirectionRadians);

        _currentWindParams = new Vector4(directionX, _windTurbulence, directionZ, scaledTime);

        _grassMaterial.SetVector(_windParamsId, _currentWindParams);
        _grassMaterial.SetFloat(_windStrengthId, _windStrength);
    }

    /// <summary>
    /// Atualiza o sistema de Texturas de renderização para amassar a grama.
    /// </summary>
    private void UpdateInteractionSystem()
    {
        if (_interactionFadeMaterial != null && _interactionMap1 != null && _interactionMap2 != null)
        {
            RenderTexture sourceMap;
            RenderTexture destinationMap;

            if (_isFirstMapActive)
            {
                sourceMap = _interactionMap1;
                destinationMap = _interactionMap2;
            }
            else
            {
                sourceMap = _interactionMap2;
                destinationMap = _interactionMap1;
            }

            Graphics.Blit(sourceMap, destinationMap, _interactionFadeMaterial);
            _grassMaterial.SetTexture("_InteractionMap", destinationMap); 
            _isFirstMapActive = !_isFirstMapActive;
        }
        else if (_playerTransform != null && _grassMaterial != null)
        {
            Vector3 playerPosition = _playerTransform.position;

            _grassMaterial.SetVector(_interactionPosId, new Vector4(playerPosition.x, playerPosition.y, playerPosition.z, _interactionStrength));
            _grassMaterial.SetFloat(_interactionRadiusId, _interactionRadius);
        }
    }

    private void ClearOldGrass()
    {
        Transform oldContainer = transform.Find("GrassContainer");
        if (oldContainer != null)
        {
            DestroyImmediate(oldContainer.gameObject);
        }
        _activeChunksList.Clear();
    }

    /// <summary>
    /// Gera uma configuração padrão via código para facilitar testes rápidos.
    /// </summary>
    [ContextMenu("Preencher Dados de Teste")]
    public void InjectDefaultTestData()
    {
        _grassLayersList = new List<GrassLayerConfig>
        {
            new GrassLayerConfig(0, true)
        };

        List<BladeSegment> basicSegmentsList = new List<BladeSegment>
        {
            new BladeSegment(0.03f, 0.5f),
            new BladeSegment(0.01f, 0.3f)
        };

        _bladeTypesList = new List<BladeType>
        {
            new BladeType("Grama Padrão (Código)", new Vector2(0.06f, 0.8f), 1f, basicSegmentsList)
        };

        Debug.Log("✅ Dados de teste padrão injetados com sucesso! Pode clicar em 'Gerar Grama'.");
    }

    private void OnDestroy()
    {
        _interactionMap1?.Release();
        _interactionMap2?.Release();
    }
}