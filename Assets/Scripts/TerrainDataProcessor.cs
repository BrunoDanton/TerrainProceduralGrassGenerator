using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Responsável pelo processamento, leitura e extração de dados matemáticos e texturas do componente Terrain.
/// </summary>
public class TerrainDataProcessor
{
    private Terrain _targetTerrain;
    private TerrainData _terrainData;
    
    private int _terrainWidth;
    private int _terrainHeight;
    private int _alphamapWidth;
    private int _alphamapHeight;
    private float[,,] _alphamaps3DArray;

    public int TerrainWidth => _terrainWidth;
    public int TerrainHeight => _terrainHeight;

    /// <summary>
    /// Inicializa as referências e extrai as dimensões e mapas de mistura (alphamaps) do terreno alvo.
    /// </summary>
    public bool TryInitialize(Terrain terrain)
    {
        _targetTerrain = terrain;
        if (_targetTerrain == null) return false;

        _terrainData = _targetTerrain.terrainData;
        if (_terrainData == null) return false;

        _terrainWidth = Mathf.RoundToInt(_terrainData.size.x);
        _terrainHeight = Mathf.RoundToInt(_terrainData.size.z);
        _alphamapWidth = _terrainData.alphamapWidth;
        _alphamapHeight = _terrainData.alphamapHeight;
        
        _alphamaps3DArray = _terrainData.GetAlphamaps(0, 0, _alphamapWidth, _alphamapHeight);

        return true;
    }

    /// <summary>
    /// Amostra a altura exata do terreno em uma determinada coordenada do mundo.
    /// </summary>
    public float GetSampledHeight(Vector3 worldPosition)
    {
        return _targetTerrain.SampleHeight(worldPosition);
    }

    /// <summary>
    /// Calcula e interpola o vetor normal do terreno com base nas coordenadas normalizadas (0 a 1).
    /// </summary>
    public Vector3 GetInterpolatedNormal(float normalizedX, float normalizedZ)
    {
        return _terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
    }

    /// <summary>
    /// Valida a presença de camadas de grama permitidas e calcula a cor interpolada das texturas do terreno.
    /// </summary>
    public bool HasValidTerrainColor(float normalizedX, float normalizedZ, List<GrassLayerConfig> configuredLayersList, float minimumTextureWeight, out Color finalColor)
    {
        int mapX = Mathf.FloorToInt(normalizedX * (_alphamapWidth - 1));
        int mapZ = Mathf.FloorToInt(normalizedZ * (_alphamapHeight - 1));

        mapX = Mathf.Clamp(mapX, 0, _alphamapWidth - 1);
        mapZ = Mathf.Clamp(mapZ, 0, _alphamapHeight - 1);

        finalColor = Color.black;
        float totalWeight = 0f;

        for (int i = 0; i < _terrainData.alphamapLayers; i++)
        {
            float currentWeight = _alphamaps3DArray[mapZ, mapX, i];
            if (currentWeight <= 0) continue;

            GrassLayerConfig layerConfig = configuredLayersList.Find(layer => layer.LayerIndex == i);
            if (layerConfig == null || !layerConfig.CanGenerateGrass) continue;

            TerrainLayer terrainLayer = _terrainData.terrainLayers[i];
            if (terrainLayer.diffuseTexture == null) continue;

            Color textureColor = terrainLayer.diffuseTexture.GetPixelBilinear(normalizedX, normalizedZ);
            finalColor += textureColor * currentWeight;
            totalWeight += currentWeight;
        }

        if (totalWeight >= minimumTextureWeight)
        {
            finalColor /= totalWeight;
            return true;
        }

        return false;
    }
}