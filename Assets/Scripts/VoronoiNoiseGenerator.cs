using UnityEngine;

/// <summary>
/// Gerador matemático de ruído Voronoi padronizado para amostragem e distribuição procedural.
/// </summary>
public static class VoronoiNoiseGenerator
{
    public enum DistanceMetricType
    {
        Euclidean,
        Manhattan,
        Chebyshev,
        Minkowski
    }

    public enum CellValueModeType
    {
        F1,
        F2,
        F2MinusF1,
        F1PlusF2,
        CellIdentifier,
        CellNoise
    }

    /// <summary>
    /// Calcula o valor base de ruído Voronoi para as coordenadas X e Z com base nos parâmetros métricos fornecidos.
    /// </summary>
    public static float CalculateNoise(
        float worldPositionX, float worldPositionZ, 
        float noiseScale = 10f,
        float pointJitter = 1f,
        CellValueModeType valueMode = CellValueModeType.F1,
        DistanceMetricType metricType = DistanceMetricType.Euclidean,
        float minkowskiPower = 2f)
    {
        worldPositionX *= noiseScale;
        worldPositionZ *= noiseScale;

        int baseCellX = Mathf.FloorToInt(worldPositionX);
        int baseCellZ = Mathf.FloorToInt(worldPositionZ);

        float minimumDistance1 = float.MaxValue;
        float minimumDistance2 = float.MaxValue;
        float cellIdentifier = 0f;
        Vector2 closestPointPosition = Vector2.zero;

        for (int offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                int neighborCellX = baseCellX + offsetX;
                int neighborCellZ = baseCellZ + offsetZ;
                Vector2 cellCoordinate = new Vector2(neighborCellX, neighborCellZ);

                Vector2 randomOffset = CalculateHash2D(cellCoordinate) * pointJitter;
                Vector2 evaluatedPointPosition = cellCoordinate + randomOffset;

                float calculatedDistance = CalculateDistanceBetweenPoints(
                    new Vector2(worldPositionX, worldPositionZ), 
                    evaluatedPointPosition, 
                    metricType, 
                    minkowskiPower
                );

                if (calculatedDistance < minimumDistance1)
                {
                    minimumDistance2 = minimumDistance1;
                    minimumDistance1 = calculatedDistance;
                    closestPointPosition = evaluatedPointPosition;
                    cellIdentifier = CalculateHash1D(cellCoordinate);
                }
                else if (calculatedDistance < minimumDistance2)
                {
                    minimumDistance2 = calculatedDistance;
                }
            }
        }

        return EvaluateCellValue(valueMode, minimumDistance1, minimumDistance2, cellIdentifier, closestPointPosition);
    }

    /// <summary>
    /// Preset otimizado para o cálculo de agrupamento orgânico (clumping) da grama procedural.
    /// </summary>
    public static float CalculateGrassClumps(float worldPositionX, float worldPositionZ, float noiseScale = 15f)
    {
        float noiseValue = CalculateNoise(worldPositionX, worldPositionZ, noiseScale, 0.9f, CellValueModeType.F1, DistanceMetricType.Euclidean);
        return 1f - Mathf.Clamp01(noiseValue);
    }

    private static float CalculateDistanceBetweenPoints(Vector2 pointA, Vector2 pointB, DistanceMetricType metricType, float powerValue)
    {
        switch (metricType)
        {
            case DistanceMetricType.Euclidean:
                return Vector2.Distance(pointA, pointB);
            case DistanceMetricType.Manhattan:
                return Mathf.Abs(pointA.x - pointB.x) + Mathf.Abs(pointA.y - pointB.y);
            case DistanceMetricType.Chebyshev:
                return Mathf.Max(Mathf.Abs(pointA.x - pointB.x), Mathf.Abs(pointA.y - pointB.y));
            case DistanceMetricType.Minkowski:
                float deltaX = Mathf.Abs(pointA.x - pointB.x);
                float deltaY = Mathf.Abs(pointA.y - pointB.y);
                return Mathf.Pow(Mathf.Pow(deltaX, powerValue) + Mathf.Pow(deltaY, powerValue), 1f / powerValue);
            default:
                return Vector2.Distance(pointA, pointB);
        }
    }

    private static float EvaluateCellValue(CellValueModeType mode, float distanceF1, float distanceF2, float cellIdentifier, Vector2 targetPoint)
    {
        switch (mode)
        {
            case CellValueModeType.F1:
                return Mathf.Clamp01(distanceF1);
            case CellValueModeType.F2:
                return Mathf.Clamp01(distanceF2);
            case CellValueModeType.F2MinusF1:
                return Mathf.Clamp01(distanceF2 - distanceF1);
            case CellValueModeType.F1PlusF2:
                return Mathf.Clamp01((distanceF1 + distanceF2) * 0.5f);
            case CellValueModeType.CellIdentifier:
                return cellIdentifier;
            case CellValueModeType.CellNoise:
                return CalculateHash1D(targetPoint);
            default:
                return Mathf.Clamp01(distanceF1);
        }
    }

    private static Vector2 CalculateHash2D(Vector2 coordinate)
    {
        const float primeK1 = 127.1f;
        const float primeK2 = 311.7f;
        const float primeK3 = 269.5f;
        const float primeK4 = 183.3f;

        float hashX = Mathf.Sin(coordinate.x * primeK1 + coordinate.y * primeK2) * 43758.5453123f;
        float hashY = Mathf.Sin(coordinate.x * primeK3 + coordinate.y * primeK4) * 43758.5453123f;

        hashX -= Mathf.Floor(hashX);
        hashY -= Mathf.Floor(hashY);

        return new Vector2(hashX, hashY);
    }

    private static float CalculateHash1D(Vector2 coordinate)
    {
        const float primeK1 = 127.1f;
        const float primeK2 = 311.7f;
        
        float hashValue = Mathf.Sin(coordinate.x * primeK1 + coordinate.y * primeK2) * 43758.5453123f;
        return hashValue - Mathf.Floor(hashValue);
    }
}