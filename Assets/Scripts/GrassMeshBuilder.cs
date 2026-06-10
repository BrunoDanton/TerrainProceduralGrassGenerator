using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Responsável por gerar os vértices, triângulos e malhas (Meshes) da grama.
/// </summary>
public class GrassMeshBuilder
{
    private readonly TerrainDataProcessor _terrainProcessor;
    private readonly Transform _parentContainerTransform;
    private readonly Material _grassMaterial;

    public GrassMeshBuilder(TerrainDataProcessor terrainProcessor, Transform parentContainerTransform, Material grassMaterial)
    {
        _terrainProcessor = terrainProcessor;
        _parentContainerTransform = parentContainerTransform;
        _grassMaterial = grassMaterial;
    }

    public List<GrassChunk> BuildGrassChunksList(
        int chunkSize, 
        int grassDensity, 
        float leafDispersion, 
        List<GrassLayerConfig> grassLayersList, 
        List<BladeType> bladeTypesList,
        float minTextureWeight,
        float perlinNoiseScale,
        float clumpingScale,
        float minimumAcceptableNoise,
        int maxVerticesPerChunk,
        float terrainNormalBlend,
        float minScaleMultiplier,
        float maxScaleMultiplier)
    {
        List<GrassChunk> generatedChunksList = new List<GrassChunk>();
        
        int chunkCountX = Mathf.CeilToInt((float)_terrainProcessor.TerrainWidth / chunkSize);
        int chunkCountZ = Mathf.CeilToInt((float)_terrainProcessor.TerrainHeight / chunkSize);

        for (int chunkX = 0; chunkX < chunkCountX; chunkX++)
        {
            for (int chunkZ = 0; chunkZ < chunkCountZ; chunkZ++)
            {
                GrassChunk chunk = GenerateSingleChunk(chunkX, chunkZ, chunkSize, grassDensity, leafDispersion, 
                    grassLayersList, bladeTypesList, minTextureWeight,
                    perlinNoiseScale, clumpingScale, minimumAcceptableNoise, maxVerticesPerChunk,
                    terrainNormalBlend, minScaleMultiplier, maxScaleMultiplier);
                if (chunk != null)
                {
                    generatedChunksList.Add(chunk);
                }
            }
        }

        return generatedChunksList;
    }

    private GrassChunk GenerateSingleChunk(
        int chunkX, int chunkZ, int chunkSize, int grassDensity, float leafDispersion, 
        List<GrassLayerConfig> grassLayersList, List<BladeType> bladeTypesList, float minTextureWeight,
        float perlinNoiseScale, float clumpingScale, float minimumAcceptableNoise, int maxVerticesPerChunk,
        float terrainNormalBlend, float minScaleMultiplier, float maxScaleMultiplier)
    {
        List<Vector3> verticesList = new List<Vector3>();
        List<int> trianglesList = new List<int>();
        List<Vector2> uvsList = new List<Vector2>();
        List<Color> colorsList = new List<Color>();

        int xStart = chunkX * chunkSize;
        int xEnd = Mathf.Min(xStart + chunkSize, _terrainProcessor.TerrainWidth);
        int zStart = chunkZ * chunkSize;
        int zEnd = Mathf.Min(zStart + chunkSize, _terrainProcessor.TerrainHeight);

        Vector3 chunkCenterPosition = Vector3.zero;
        int grassCount = 0;

        for (int x = xStart; x < xEnd; x++)
        {
            for (int z = zStart; z < zEnd; z++)
            {
                if (verticesList.Count >= maxVerticesPerChunk) break;

                // Pula células que não passam nos filtros de ruído e textura
                if (!TryGetCellData(x, z, perlinNoiseScale, clumpingScale, minimumAcceptableNoise,
                    grassLayersList, minTextureWeight, out Color terrainColor, out Vector3 basePosition, out Quaternion blendedRotation, terrainNormalBlend))
                    continue;

                PopulateCellWithBlades(verticesList, trianglesList, uvsList, colorsList,
                    basePosition, blendedRotation, terrainColor,
                    grassDensity, leafDispersion, minScaleMultiplier, maxScaleMultiplier,
                    bladeTypesList, maxVerticesPerChunk,
                    ref chunkCenterPosition, ref grassCount);
            }
        }

        if (verticesList.Count > 0)
        {
            chunkCenterPosition /= grassCount;
            return CreateChunkGameObject(chunkX, chunkZ, verticesList, trianglesList, uvsList, colorsList, chunkCenterPosition);
        }

        return null;
    }

    /// <summary>
    /// Valida se uma célula do terreno deve receber grama e extrai os dados necessários para o posicionamento.
    /// Retorna false se o ruído ou a textura não atingirem os limiares mínimos.
    /// </summary>
    private bool TryGetCellData(
        int x, int z,
        float perlinNoiseScale, float clumpingScale, float minimumAcceptableNoise,
        List<GrassLayerConfig> grassLayersList, float minTextureWeight,
        out Color terrainColor, out Vector3 basePosition, out Quaternion blendedRotation,
        float terrainNormalBlend)
    {
        terrainColor = Color.black;
        basePosition = Vector3.zero;
        blendedRotation = Quaternion.identity;

        float normalizedX = (float)x / _terrainProcessor.TerrainWidth;
        float normalizedZ = (float)z / _terrainProcessor.TerrainHeight;

        if (!PassesNoiseFilter(normalizedX, normalizedZ, perlinNoiseScale, clumpingScale, minimumAcceptableNoise))
            return false;

        if (!_terrainProcessor.HasValidTerrainColor(normalizedX, normalizedZ, grassLayersList, minTextureWeight, out terrainColor))
            return false;

        float worldX = normalizedX * _terrainProcessor.TerrainWidth;
        float worldZ = normalizedZ * _terrainProcessor.TerrainHeight;
        float worldY = _terrainProcessor.GetSampledHeight(new Vector3(worldX, 0, worldZ));

        basePosition = new Vector3(worldX, worldY, worldZ);

        // Mescla a rotação da inclinação do terreno com a vertical absoluta (solução para encostas íngremes)
        Vector3 terrainNormal = _terrainProcessor.GetInterpolatedNormal(normalizedX, normalizedZ);
        Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, terrainNormal);
        blendedRotation = Quaternion.Slerp(Quaternion.identity, slopeRotation, terrainNormalBlend);

        return true;
    }

    /// <summary>
    /// Calcula o score combinado de Perlin e Voronoi e verifica se ultrapassa o limiar mínimo.
    /// </summary>
    private bool PassesNoiseFilter(
        float normalizedX, float normalizedZ,
        float perlinNoiseScale, float clumpingScale, float minimumAcceptableNoise)
    {
        float baseNoise = Mathf.PerlinNoise(normalizedX * perlinNoiseScale, normalizedZ * perlinNoiseScale);
        float clumpingNoise = VoronoiNoiseGenerator.CalculateGrassClumps(normalizedX, normalizedZ, clumpingScale);
        float finalNoiseScore = baseNoise * Mathf.Lerp(1.0f, clumpingNoise, 0.5f);

        return finalNoiseScore > minimumAcceptableNoise;
    }

    /// <summary>
    /// Itera pela densidade configurada e planta lâminas individuais em uma célula aprovada.
    /// Atualiza o centro acumulado do chunk e o contador total de lâminas por referência.
    /// </summary>
    private void PopulateCellWithBlades(
        List<Vector3> verticesList, List<int> trianglesList, List<Vector2> uvsList, List<Color> colorsList,
        Vector3 basePosition, Quaternion blendedRotation, Color terrainColor,
        int grassDensity, float leafDispersion, float minScaleMultiplier, float maxScaleMultiplier,
        List<BladeType> bladeTypesList, int maxVerticesPerChunk,
        ref Vector3 chunkCenterPosition, ref int grassCount)
    {
        for (int i = 0; i < grassDensity; i++)
        {
            if (verticesList.Count >= maxVerticesPerChunk) break;

            BladeType selectedType = bladeTypesList[0];

            float plantProbabilityChance = Mathf.Clamp01(selectedType.DensityMultiplier / 5f);
            if (Random.value > plantProbabilityChance) continue;

            Vector3 instancePosition = CalculateBladePosition(basePosition, leafDispersion);

            Quaternion yawRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            Quaternion finalRotation = blendedRotation * yawRotation;

            // Aplica uma escala orgânica aleatória para cada lâmina
            float randomScale = Random.Range(minScaleMultiplier, maxScaleMultiplier);

            BuildBladeGeometry(verticesList, trianglesList, uvsList, colorsList,
                instancePosition, finalRotation, terrainColor, randomScale, selectedType);

            chunkCenterPosition += instancePosition;
            grassCount++;
        }
    }

    /// <summary>
    /// Calcula a posição final de uma lâmina aplicando dispersão aleatória e reamostrando a altura do terreno.
    /// </summary>
    private Vector3 CalculateBladePosition(Vector3 basePosition, float leafDispersion)
    {
        Vector3 instancePosition = basePosition + new Vector3(
            Random.Range(-leafDispersion, leafDispersion), 0,
            Random.Range(-leafDispersion, leafDispersion)
        );
        instancePosition.y = _terrainProcessor.GetSampledHeight(instancePosition);

        return instancePosition;
    }

    /// <summary>
    /// Prepara a cor inicial e inicia o processo recursivo de construção da lâmina.
    /// </summary>
    private void BuildBladeGeometry(
        List<Vector3> verticesList, List<int> trianglesList, List<Vector2> uvsList, List<Color> colorsList,
        Vector3 position, Quaternion rotation, Color baseColor, float scale, BladeType type)
    {
        float hue;
        float saturation;
        float brightnessValue;
        
        Color.RGBToHSV(baseColor, out hue, out saturation, out brightnessValue);

        int baseVertexIndex = verticesList.Count;

        BuildBladeRecursive(
            verticesList, trianglesList, uvsList, colorsList, 
            baseVertexIndex, position, rotation, 
            hue, saturation, brightnessValue,
            0, type.BladeSize.x * scale, 0f, 0f, type.BladeSize.y * scale, type.BladeSize.x * scale, type
        );
    }

    /// <summary>
    /// Função recursiva que constrói os segmentos verticais de uma única lâmina de grama.
    /// </summary>
    private void BuildBladeRecursive(
        List<Vector3> verticesList, List<int> trianglesList, List<Vector2> uvsList, List<Color> colorsList,
        int baseVertexIndex, Vector3 position, Quaternion rotation,
        float hue, float saturation, float brightnessValue,
        int segmentIndex, float baseWidth, float baseHeight,
        float accumHeightPercent, float totalBladeHeight, float originalBaseWidth, BladeType type)
    {
        if (baseHeight == 0f)
        {
            position.y = _terrainProcessor.GetSampledHeight(position);
        }

        Vector3 baseLeftVertex = new Vector3(-baseWidth / 2, baseHeight, 0);
        Vector3 baseRightVertex = new Vector3(baseWidth / 2, baseHeight, 0);

        verticesList.Add(position + rotation * baseLeftVertex);
        verticesList.Add(position + rotation * baseRightVertex);

        float baseVertexBrightness = type.HasGradient
            ? Mathf.Lerp(type.BaseBrightness, type.TipBrightness, baseHeight / totalBladeHeight)
            : 1f;

        // Sombreamento básico na base (Fake Ambient Occlusion)
        bool shouldUseAmbientOcclusion = true; 
        float ambientOcclusionIntensity = 0.3f;
        
        if (shouldUseAmbientOcclusion && baseHeight < totalBladeHeight * 0.2f)
        {
            float ambientOcclusionFactor = Mathf.Lerp(1f - ambientOcclusionIntensity, 1f, baseHeight / (totalBladeHeight * 0.2f));
            baseVertexBrightness *= ambientOcclusionFactor;
        }

        float vertexHeightNormalized = baseHeight / totalBladeHeight;
        Color vertexColor = Color.HSVToRGB(hue, saturation, brightnessValue * baseVertexBrightness);
        vertexColor.a = vertexHeightNormalized;

        colorsList.Add(vertexColor);
        colorsList.Add(vertexColor);

        float leftUv = 0.5f - (baseWidth / originalBaseWidth) / 2f;
        float rightUv = 0.5f + (baseWidth / originalBaseWidth) / 2f;
        uvsList.Add(new Vector2(leftUv, accumHeightPercent));
        uvsList.Add(new Vector2(rightUv, accumHeightPercent));

        int currentBaseVertexIndex = verticesList.Count - 2;

        if (segmentIndex >= type.SegmentsList.Count)
        {
            Vector3 tipVertex = new Vector3(0, totalBladeHeight, 0);
            verticesList.Add(position + rotation * tipVertex);

            float tipBrightness = type.HasGradient ? type.TipBrightness : 1f;
            Color tipColor = Color.HSVToRGB(hue, saturation, brightnessValue * tipBrightness);
            tipColor.a = 1f;
            
            colorsList.Add(tipColor);
            uvsList.Add(new Vector2(0.5f, 1f));

            int tipIndex = verticesList.Count - 1;
            trianglesList.Add(currentBaseVertexIndex);
            trianglesList.Add(tipIndex);
            trianglesList.Add(currentBaseVertexIndex + 1);
            
            return;
        }

        BladeSegment currentSegment = type.SegmentsList[segmentIndex];
        float topWidth = currentSegment.TopVerticesDistance;
        float newAccumHeightPercent = Mathf.Min(1f, accumHeightPercent + currentSegment.HeightPercentage);
        float topHeight = totalBladeHeight * newAccumHeightPercent;

        Vector3 topLeftVertex = new Vector3(-topWidth / 2, topHeight, 0);
        Vector3 topRightVertex = new Vector3(topWidth / 2, topHeight, 0);
        
        verticesList.Add(position + rotation * topLeftVertex);
        verticesList.Add(position + rotation * topRightVertex);

        float topVertexBrightness = type.HasGradient
            ? Mathf.Lerp(type.BaseBrightness, type.TipBrightness, topHeight / totalBladeHeight)
            : 1f;

        float vertexHeightNormalizedTop = topHeight / totalBladeHeight;
        Color topColor = Color.HSVToRGB(hue, saturation, brightnessValue * topVertexBrightness);
        topColor.a = vertexHeightNormalizedTop;
        
        colorsList.Add(topColor);
        colorsList.Add(topColor);

        float topLeftUv = 0.5f - (topWidth / originalBaseWidth) / 2f;
        float topRightUv = 0.5f + (topWidth / originalBaseWidth) / 2f;
        uvsList.Add(new Vector2(topLeftUv, newAccumHeightPercent));
        uvsList.Add(new Vector2(topRightUv, newAccumHeightPercent));

        int topVertexIndex = verticesList.Count - 2;
        
        trianglesList.Add(currentBaseVertexIndex);
        trianglesList.Add(topVertexIndex);
        trianglesList.Add(currentBaseVertexIndex + 1);
        
        trianglesList.Add(topVertexIndex);
        trianglesList.Add(topVertexIndex + 1);
        trianglesList.Add(currentBaseVertexIndex + 1);

        BuildBladeRecursive(
            verticesList, trianglesList, uvsList, colorsList, 
            baseVertexIndex, position, rotation, 
            hue, saturation, brightnessValue,
            segmentIndex + 1, topWidth, topHeight, 
            newAccumHeightPercent, totalBladeHeight, originalBaseWidth, type
        );
    }

    private GrassChunk CreateChunkGameObject(
        int gridX, int gridZ, List<Vector3> verticesList, List<int> trianglesList, 
        List<Vector2> uvsList, List<Color> colorsList, Vector3 centerPosition)
    {
        GameObject chunkObject = new GameObject($"GrassChunk_{gridX}_{gridZ}");
        chunkObject.transform.SetParent(_parentContainerTransform);
        chunkObject.transform.position = centerPosition;

        Mesh chunkMesh = new Mesh
        {
            name = $"GrassMesh_{gridX}_{gridZ}"
        };

        if (verticesList.Count > 65000)
            chunkMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        chunkObject.transform.position = centerPosition;
        for (int i = 0; i < verticesList.Count; i++)
            verticesList[i] -= centerPosition;

    chunkMesh.SetVertices(verticesList);
        chunkMesh.SetTriangles(trianglesList, 0);
        chunkMesh.SetUVs(0, uvsList);
        chunkMesh.SetColors(colorsList);
        chunkMesh.RecalculateNormals();
        chunkMesh.RecalculateBounds();

        MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = chunkMesh;

        MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = _grassMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = true;

        return new GrassChunk(chunkObject, meshRenderer, meshRenderer.bounds, centerPosition);
    }
}