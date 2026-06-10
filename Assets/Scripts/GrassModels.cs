using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuração das camadas do terreno permitidas para a geração de grama.
/// </summary>
[Serializable]
public class GrassLayerConfig
{
    [SerializeField] private string _layerName = "Nova Camada";
    [SerializeField] private int _layerIndex;
    [SerializeField] private bool _canGenerateGrass = true;

    public int LayerIndex => _layerIndex;
    public bool CanGenerateGrass => _canGenerateGrass;

    public GrassLayerConfig(int layerIndex, bool canGenerate = true)
    {
        _layerName = "Camada de Teste " + layerIndex;
        _layerIndex = layerIndex;
        _canGenerateGrass = canGenerate;
    }
}

/// <summary>
/// Define a estrutura geométrica de um segmento individual da lâmina de grama.
/// </summary>
[Serializable]
public class BladeSegment
{
    [SerializeField] private float _topVerticesDistance;
    [Range(0.01f, 1f)] [SerializeField] private float _heightPercentage;

    public float TopVerticesDistance => _topVerticesDistance;
    public float HeightPercentage => _heightPercentage;

    public BladeSegment(float topDistance, float heightPercent)
    {
        _topVerticesDistance = topDistance;
        _heightPercentage = heightPercent;
    }
}

/// <summary>
/// Configuração visual, dimensional e de densidade de um tipo específico de grama.
/// </summary>
[Serializable]
public class BladeType
{
    [SerializeField] private string _name = "Tipo de Lâmina";
    
    [Header("Forma")]
    [SerializeField] private Vector2 _bladeSize = new Vector2(0.05f, 1f);
    [SerializeField] private List<BladeSegment> _segmentsList = new List<BladeSegment>();
    
    [Header("Aparência")]
    [SerializeField] private bool _hasGradient = true;
    [Range(0f, 1f)] [SerializeField] private float _baseBrightness = 0.3f;
    [Range(0f, 1f)] [SerializeField] private float _tipBrightness = 1f;
    
    [Header("Densidade Relativa")]
    [Range(0.1f, 5f)] [SerializeField] private float _densityMultiplier = 1f;

    public string Name => _name;
    public Vector2 BladeSize => _bladeSize;
    public List<BladeSegment> SegmentsList => _segmentsList;
    public bool HasGradient => _hasGradient;
    public float BaseBrightness => _baseBrightness;
    public float TipBrightness => _tipBrightness;
    public float DensityMultiplier => _densityMultiplier;

    public BladeType(string name, Vector2 size, float density, List<BladeSegment> segments)
    {
        _name = name;
        _bladeSize = size;
        _densityMultiplier = density;
        _segmentsList = segments != null ? segments : new List<BladeSegment>();
        _hasGradient = true;
        _baseBrightness = 0.3f;
        _tipBrightness = 1f;
    }
}

/// <summary>
/// Representa um bloco ou agrupamento de grama gerado para otimização de Culling e performance.
/// </summary>
public class GrassChunk
{
    public GameObject ChunkGameObject { get; private set; }
    public MeshRenderer ChunkRenderer { get; private set; }
    public Vector3 CenterPosition { get; private set; }
    public float DistanceToCamera { get; set; }
    public Bounds ChunkBounds { get; private set; } 

    public GrassChunk(GameObject chunkObject, MeshRenderer renderer, Bounds bounds, Vector3 center)
    {
        ChunkGameObject = chunkObject;
        ChunkRenderer = renderer;
        CenterPosition = center;
        ChunkBounds = bounds; 
    }
}