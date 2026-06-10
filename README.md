# Procedural Grass Generator para Unity
![License](https://img.shields.io/badge/License-MIT-green)
![Unity Version](https://img.shields.io/badge/Unity-2021.3%2B-blueviolet)

## Visão Geral
Este projeto é um gerador procedural de grama otimizado e modular para a Unity Engine. Desenvolvido com foco em performance e renderização de nível comercial (AAA), o sistema utiliza a CPU para calcular vértices e malhas dinamicamente sobre o componente Terrain da Unity. O projeto adota princípios SOLID e código limpo, sendo facilmente escalável e integrável em pipelines modernos de renderização (URP e HDRP).

## Funcionalidades Principais
* **Geração Baseada em Textura:** A grama nasce apenas em áreas pintadas com texturas específicas do terreno.
* **Ruído Orgânico (Clumping):** Utiliza uma combinação matemática de Perlin Noise e Voronoi Noise para criar agrupamentos naturais, clareiras e caminhos de terra de forma processual.
* **Culling Avançado:** Implementa Frustum Culling e Distance Culling independentes por Chunk, renderizando apenas o que a câmera consegue ver.
* **Malhas Adaptativas:** Suporta a divisão dinâmica de malhas (Meshes) em 16-bits ou 32-bits, permitindo aglomerados densos sem quebrar o limite padrão da Unity.
* **Sistema de Vento:** Passagem de parâmetros de tempo, direção e turbulência via script diretamente para as propriedades do Shader da grama.
* **Interação Híbrida:** Suporta a deformação física da grama baseada na posição do jogador, utilizando mapas de textura em tempo real (RenderTextures com efeito de Fade) ou uma solução secundária por vetores de raio.
* **Geometria Customizável:** Permite a construção de múltiplos tipos de lâminas de grama compostas por segmentos ajustáveis via Inspector.

---

## Como Configurar do Zero

### 1. Preparação do Projeto e Materiais
1. Certifique-se de estar utilizando um projeto configurado com **URP (Universal Render Pipeline)** ou **HDRP**.
2. Crie uma pasta para organizar o sistema (ex: `Assets/ProceduralGrass`).
3. Importe os scripts C# para a sua pasta de scripts. **Aviso:** O arquivo `TerrainGrassGeneratorEditor.cs` deve estar obrigatoriamente dentro de uma subpasta chamada `Editor`.

### 2. Configuração do Shader e Material Principal
1. Crie um Shader Graph (Lit ou Unlit, dependendo da necessidade de iluminação) compatível com sua pipeline.
2. Nas configurações do Shader Graph (Graph Inspector), marque a opção **Two Sided** (Dois Lados) como verdadeira.
3. O Shader deve receber as seguintes propriedades expostas (nomes de referência no Blackboard):
   * `_WindParams` (Vector4)
   * `_WindStrength` (Float)
   * `_InteractionPos` (Vector4)
   * `_InteractionRadius` (Float)
   * `_InteractionMap` (Texture2D)
4. O Shader deve ler a cor através do nó **Vertex Color** e misturar com a cor base ou textura. O canal Alpha do Vertex Color dita a altura da grama e deve ser usado como máscara para o deslocamento do vento.
5. Crie um Material a partir deste Shader e nomeie-o como `M_Grass`.
6. Crie um material vazio genérico e nomeie-o como `M_InteractionFade` (ele será usado para operações de Blit interno das texturas de rastro).

### 3. Configuração do Terreno
1. Adicione um componente **Terrain** à sua cena (`GameObject > 3D Object > Terrain`).
2. Adicione Terrain Layers (texturas de chão) ao terreno e pinte-o. Identifique o índice da textura onde deseja que a grama cresça (a primeira textura é o Índice 0).
3. Adicione o componente script `Grass Generation Controller` ao mesmo objeto que possui o Terrain.

---

## Guia de Parâmetros do Inspector

### 1. Configurações Base
* **Camadas do Terreno:** Lista de índices das texturas do terreno. Defina o 'Layer Index' correspondente à textura de terra/grama e marque 'Can Generate Grass' como verdadeiro.
* **Tipos de Lâminas:** Define a geometria visual da grama.
  * **Forma:** Altura, largura e número de segmentos verticais (para curvar com o vento).
  * **Aparência:** Cores, brilho e suporte a gradiente.
  * **Densidade Relativa:** Multiplicador de chance de esta lâmina específica nascer em comparação com as outras.

### 2. Densidade e Posicionamento
* **Tamanho do Chunk:** Define as dimensões na grade X e Z de cada bloco de renderização (ex: 64x64). Chunks menores aumentam a precisão do Culling, mas geram mais objetos na hierarquia.
* **Densidade de Lâminas:** *Aviso Crítico.* Este não é um valor absoluto, mas sim um multiplicador iterativo por metro quadrado. Valores acima de 3 podem causar tempos de geração longos em terrenos extensos.
* **Dispersão das Lâminas:** O raio de espalhamento aleatório a partir do ponto central calculado para cada lâmina.
* **Limite de Vértices/Chunk:** *Aviso de Performance.* Define quando o sistema deve parar de adicionar grama a um bloco. O limite nativo do Unity para malhas de 16-bits é 65.000. Se este valor for maior, o sistema converterá a malha para 32-bits (UInt32) automaticamente.

### 3. Distribuição e Ruído (Clumping)
* **Escala do Perlin Noise:** Controla o tamanho de manchas suaves de grama através do terreno geral.
* **Escala de Clumping (Voronoi):** Controla o ruído de agrupamento. Gera efeitos visuais de "tufos" densos separados por caminhos curtos ou clareiras.
* **Corte de Ruído (Threshold):** Varia de 0.0 a 1.0. Valores maiores forçam a grama a nascer apenas nas zonas mais intensas do ruído, criando áreas completamente vazias no meio do campo.

### 4. Culling e Performance
* **Distância Máxima:** Distância em unidades (metros) entre a câmera e o centro do chunk. Além desta distância, o MeshRenderer do chunk é desativado para economizar processamento.
* **Usar Frustum Culling:** Se ativo, oculta os chunks que não estão atualmente dentro do cone de visão da câmera (mesmo que estejam perto), poupando carga considerável na placa de vídeo ao girar a visão.

### 5. Vento e Interação
* **Habilitar Vento:** Revela os controles de Velocidade, Força, Direção (em graus, 0 a 360) e Turbulência. Estes dados são empacotados e enviados ao Shader a cada frame.
* **Habilitar Interação:** Ativa o sistema de reação física ao jogador.
  * **Transform do Jogador:** O objeto que vai amassar a relva.
  * **Raio de Interação:** Distância de influência ao redor do transform do jogador.
  * **Força da Interação:** O quanto a grama é dobrada na direção oposta.

### 6. Materiais
* **Material da Grama:** Insira o material `M_Grass` configurado anteriormente.
* **Material de Fade:** Insira o `M_InteractionFade`. O sistema utiliza este material intermediário para mesclar (Blit) duas RenderTextures e criar um efeito de memória/rastro onde o jogador pisou.

### 7. Configurações Avançadas (Engine)
* **Alinhamento com Encosta:** Interpolação suave (Slerp) entre o vetor vertical absoluto (0) e a normal da inclinação do terreno (1). Útil para evitar que a grama fique completamente deitada em montanhas muito íngremes.
* **Escala Mínima / Máxima:** Multiplicadores aleatórios aplicados a cada lâmina gerada para quebrar o padrão de repetição da geometria.
* **Margem do Frustum (Padding):** Expande artificialmente a caixa de colisão virtual do chunk. Resolve o problema da grama piscar nas extremidades da tela devido às distorções laterais causadas pelo deslocamento de vento na placa de vídeo.

---

## Execução
Após a configuração, basta pressionar o botão **Gerar Grama** presente no Inspector. O sistema processará os dados do terreno e instanciará os chunks como filhos de um objeto vazio chamado `GrassContainer`. Para limpar a grama gerada via código, gere novamente, o sistema apagará automaticamente o contêiner antigo.