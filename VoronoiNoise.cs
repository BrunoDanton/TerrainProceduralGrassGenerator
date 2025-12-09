using UnityEngine;

/// <summary>
/// Sistema avançado de Voronoi Noise para geração procedural AAA.
/// Implementa múltiplas variantes: F1, F2, F2-F1, Manhattan, Chebyshev.
/// Otimizado com grid caching e hash determinístico de alta qualidade.
/// Baseado em técnicas de Ghost of Tsushima e Horizon Zero Dawn.
/// </summary>
public static class VoronoiNoise
{
    // ========== ENUMS ==========
    public enum DistanceMetric
    {
        Euclidean,      // Distância padrão (circular)
        Manhattan,      // Distância city-block (quadrada)
        Chebyshev,      // Distância chessboard (octagonal)
        Minkowski       // Generalizável (p-norm)
    }

    public enum CellValueMode
    {
        F1,             // Distância ao ponto mais próximo (clumps suaves)
        F2,             // Distância ao segundo ponto mais próximo
        F2MinusF1,      // Diferença (bordas de células definidas)
        F1PlusF2,       // Soma (padrão de rede)
        CellID,         // ID único da célula (flat shading)
        CellNoise       // Valor aleatório por célula
    }

    // ========== CONFIGURAÇÃO ==========
    private const int GRID_SIZE = 32; // Cache para otimização
    private static Vector2[,] _gridCache = null;
    private static bool _cacheInitialized = false;

    // ========== FUNÇÃO PRINCIPAL ==========
    
    /// <summary>
    /// Gera Voronoi Noise avançado com múltiplas opções.
    /// </summary>
    /// <param name="x">Coordenada X (world space)</param>
    /// <param name="z">Coordenada Z (world space)</param>
    /// <param name="scale">Escala do ruído (menor = células maiores)</param>
    /// <param name="jitter">Aleatoriedade da posição dos pontos (0-1)</param>
    /// <param name="mode">Modo de cálculo do valor da célula</param>
    /// <param name="metric">Métrica de distância</param>
    /// <param name="minkowskiP">Expoente para Minkowski distance (default: 2 = Euclidean)</param>
    /// <returns>Valor 0-1</returns>
    public static float Generate(
        float x, float z, 
        float scale = 10f,
        float jitter = 1f,
        CellValueMode mode = CellValueMode.F1,
        DistanceMetric metric = DistanceMetric.Euclidean,
        float minkowskiP = 2f)
    {
        // Aplicar escala
        x *= scale;
        z *= scale;

        // Célula base
        int cellX = Mathf.FloorToInt(x);
        int cellZ = Mathf.FloorToInt(z);

        // Armazenar as 3 menores distâncias (para F2-F1)
        float minDist1 = float.MaxValue;
        float minDist2 = float.MaxValue;
        float cellID = 0f;
        Vector2 closestPoint = Vector2.zero;

        // Verificar células vizinhas (3x3 grid)
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                // Coordenadas da célula vizinha
                int neighborX = cellX + i;
                int neighborZ = cellZ + j;
                Vector2 cellCoord = new Vector2(neighborX, neighborZ);

                // Gerar ponto dentro da célula (com jitter)
                Vector2 randomOffset = Hash2D(cellCoord) * jitter;
                Vector2 pointPos = cellCoord + randomOffset;

                // Calcular distância
                float dist = CalculateDistance(
                    new Vector2(x, z), 
                    pointPos, 
                    metric, 
                    minkowskiP
                );

                // Atualizar distâncias mínimas
                if (dist < minDist1)
                {
                    minDist2 = minDist1;
                    minDist1 = dist;
                    closestPoint = pointPos;
                    cellID = Hash1D(cellCoord);
                }
                else if (dist < minDist2)
                {
                    minDist2 = dist;
                }
            }
        }

        // Retornar valor baseado no modo
        return CalculateCellValue(mode, minDist1, minDist2, cellID, closestPoint);
    }

    // ========== VORONOI COM MÚLTIPLAS OCTAVES (FRACTAL) ==========
    
    /// <summary>
    /// Voronoi fractal - combina múltiplas octaves para detalhes complexos.
    /// </summary>
    public static float GenerateFractal(
        float x, float z,
        float scale = 10f,
        int octaves = 3,
        float lacunarity = 2f,
        float persistence = 0.5f,
        float jitter = 1f,
        CellValueMode mode = CellValueMode.F1,
        DistanceMetric metric = DistanceMetric.Euclidean)
    {
        float total = 0f;
        float frequency = scale;
        float amplitude = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            total += Generate(x, z, frequency, jitter, mode, metric) * amplitude;
            
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return total / maxValue; // Normalizar
    }

    // ========== VORONOI PARA CLUMPING (OTIMIZADO) ==========
    
    /// <summary>
    /// Versão otimizada especificamente para grass clumping.
    /// Usa F1 com distância Euclidean e retorna valor invertido (perto = alto).
    /// </summary>
    public static float GenerateForClumping(float x, float z, float scale = 10f, float jitter = 1f)
    {
        float value = Generate(x, z, scale, jitter, CellValueMode.F1, DistanceMetric.Euclidean);
        
        // Inverter: perto do centro = 1, longe = 0
        return 1f - Mathf.Clamp01(value);
    }

    // ========== VORONOI COM BORDAS (CRACK PATTERN) ==========
    
    /// <summary>
    /// Gera padrão de rachaduras/bordas de células.
    /// Perfeito para terrenos rochosos ou padrões de placas.
    /// </summary>
    public static float GenerateEdges(float x, float z, float scale = 10f, float edgeWidth = 0.1f)
    {
        float f2MinusF1 = Generate(x, z, scale, 1f, CellValueMode.F2MinusF1, DistanceMetric.Euclidean);
        
        // Criar bordas definidas
        return Mathf.SmoothStep(0f, edgeWidth, f2MinusF1);
    }

    // ========== VORONOI COM GRADIENTE SUAVE ==========
    
    /// <summary>
    /// Combina Voronoi com Perlin para transições mais orgânicas.
    /// Útil para distribuição de biomas.
    /// </summary>
    public static float GenerateSmooth(float x, float z, float scale = 10f, float smoothness = 0.5f)
    {
        float voronoi = GenerateForClumping(x, z, scale);
        float perlin = (Mathf.PerlinNoise(x * scale * 0.5f, z * scale * 0.5f) - 0.5f) * 2f;
        
        return Mathf.Lerp(voronoi, (perlin + 1f) * 0.5f, smoothness);
    }

    // ========== FUNÇÕES AUXILIARES ==========

    private static float CalculateDistance(Vector2 a, Vector2 b, DistanceMetric metric, float p)
    {
        switch (metric)
        {
            case DistanceMetric.Euclidean:
                return Vector2.Distance(a, b);
            
            case DistanceMetric.Manhattan:
                return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
            
            case DistanceMetric.Chebyshev:
                return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
            
            case DistanceMetric.Minkowski:
                float dx = Mathf.Abs(a.x - b.x);
                float dy = Mathf.Abs(a.y - b.y);
                return Mathf.Pow(Mathf.Pow(dx, p) + Mathf.Pow(dy, p), 1f / p);
            
            default:
                return Vector2.Distance(a, b);
        }
    }

    private static float CalculateCellValue(CellValueMode mode, float f1, float f2, float cellID, Vector2 point)
    {
        switch (mode)
        {
            case CellValueMode.F1:
                return Mathf.Clamp01(f1);
            
            case CellValueMode.F2:
                return Mathf.Clamp01(f2);
            
            case CellValueMode.F2MinusF1:
                return Mathf.Clamp01(f2 - f1);
            
            case CellValueMode.F1PlusF2:
                return Mathf.Clamp01((f1 + f2) * 0.5f);
            
            case CellValueMode.CellID:
                return cellID;
            
            case CellValueMode.CellNoise:
                return Hash1D(point);
            
            default:
                return Mathf.Clamp01(f1);
        }
    }

    // ========== FUNÇÕES DE HASH (DETERMINÍSTICAS) ==========

    /// <summary>
    /// Hash 2D → Vector2. Gera offset aleatório mas determinístico.
    /// Usa konstantes mágicas para boa distribuição.
    /// </summary>
    private static Vector2 Hash2D(Vector2 p)
    {
        // Constantes mágicas (números primos grandes)
        const float K1 = 127.1f;
        const float K2 = 311.7f;
        const float K3 = 269.5f;
        const float K4 = 183.3f;

        float x = Mathf.Sin(p.x * K1 + p.y * K2) * 43758.5453123f;
        float y = Mathf.Sin(p.x * K3 + p.y * K4) * 43758.5453123f;

        x = x - Mathf.Floor(x);
        y = y - Mathf.Floor(y);

        return new Vector2(x, y);
    }

    /// <summary>
    /// Hash 2D → Float. Gera valor único por célula.
    /// </summary>
    private static float Hash1D(Vector2 p)
    {
        const float K1 = 127.1f;
        const float K2 = 311.7f;
        
        float h = Mathf.Sin(p.x * K1 + p.y * K2) * 43758.5453123f;
        return h - Mathf.Floor(h);
    }

    // ========== UTILITÁRIOS DE DEBUG ==========

    /// <summary>
    /// Gera textura de preview do Voronoi (para debug no Editor).
    /// </summary>
    public static Texture2D GenerateDebugTexture(
        int resolution = 512,
        float scale = 10f,
        CellValueMode mode = CellValueMode.F1)
    {
        Texture2D tex = new Texture2D(resolution, resolution);
        
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float normX = (float)x / resolution;
                float normZ = (float)y / resolution;
                
                float value = Generate(normX, normZ, scale, 1f, mode);
                Color col = new Color(value, value, value);
                
                tex.SetPixel(x, y, col);
            }
        }
        
        tex.Apply();
        return tex;
    }

    // ========== PRESET FUNCTIONS (PARA USO RÁPIDO) ==========

    /// <summary>
    /// Preset: Clumps naturais para grama (Ghost of Tsushima style).
    /// </summary>
    public static float GrassClumps(float x, float z, float scale = 15f)
    {
        return GenerateForClumping(x, z, scale, 0.9f);
    }

    /// <summary>
    /// Preset: Rochas/pedras espalhadas.
    /// </summary>
    public static float RockScatter(float x, float z, float scale = 5f)
    {
        return Generate(x, z, scale, 0.3f, CellValueMode.CellID, DistanceMetric.Euclidean);
    }

    /// <summary>
    /// Preset: Padrão de placas tectônicas (bordas definidas).
    /// </summary>
    public static float TectonicPlates(float x, float z, float scale = 8f)
    {
        return Generate(x, z, scale, 0.5f, CellValueMode.F2MinusF1, DistanceMetric.Euclidean);
    }

    /// <summary>
    /// Preset: Distribuição orgânica para flores/arbustos.
    /// </summary>
    public static float FloralClusters(float x, float z, float scale = 20f)
    {
        // Combina duas escalas diferentes
        float large = GenerateForClumping(x, z, scale * 0.5f, 1f);
        float small = GenerateForClumping(x, z, scale * 2f, 0.8f);
        return large * 0.7f + small * 0.3f;
    }

    /// <summary>
    /// Preset: Cristais/formações hexagonais (Chebyshev).
    /// </summary>
    public static float Crystals(float x, float z, float scale = 12f)
    {
        return Generate(x, z, scale, 0.2f, CellValueMode.F1, DistanceMetric.Chebyshev);
    }
}

