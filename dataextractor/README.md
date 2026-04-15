# DataExtractor (ESAPI) — Extração de DTH/DVH

Solução **C#/.NET (ESAPI – Eclipse / Varian)** para extrair, a partir de planos de radioterapia:

- **DTH (Distance-To-Target Histogram)** do OAR em relação ao volume alvo (PTV) em duas componentes:
  - `dthIn_*`  → pontos do OAR **dentro** dos planos axiais do alvo (PTV)
  - `dthOut_*` → pontos do OAR **fora** dos planos axiais do alvo (PTV)

- **DVH** do OAR (pontos **dose×volume**, cumulativo)

A solução foi desenhada para gerar **três arquivos**:

1. Um arquivo com as *features* (DTHs e volume do PTV) - sem identificadores do paciente para análise/modelagem.
2. Um arquivo com os pontos do DVH do OAR, também sem identificadores do paciente, contendo pares **dose–volume**, onde **dose** atuará como uma das *features* (juntamente com DTHs e volume do PTV), e **volume** será a variável *target*.
3. Um arquivo separado com os identificadores necessários para auditoria/rastreabilidade (`patient_id`, `course` e `plan`).

---

## Como a solução funciona (visão geral)

1. O usuário seleciona um CSV de entrada com os **casos** (paciente/curso/plano/PTV).
2. O usuário seleciona um CSV com **termos de busca** do OAR (ex.: `esofago`, `esophagus`, etc.).
3. Para cada linha do CSV de entrada:
   - O script abre o paciente no Eclipse (ESAPI).
   - Localiza o **Course** e o **PlanSetup** informados.
   - Localiza o **PTV** pelo `Id`.
   - Localiza o **OAR** por termo(s) de busca (incluindo lista opcional de termos de exclusão).
   - Obtem:
     - volume do PTV
     - DTH IN/OUT (usando bins definidos em `settings.json`)
     - DVH (cumulativo) do OAR (usando bins/resolução e Dose Máxima também definidos em `settings.json`)
   - Escreve as saídas em CSV.

---

## Entradas

### 1) CSV de casos (selecionado via diálogo)

Formato esperado (separador `;` ou `,`):

```text
patient_id;course_id;caso_id;ptv_id
```

Exemplo:

```text
568435;C1;PlanA;PTV1_3x18Gy
```

### 2) CSV com termos de busca do OAR (selecionado via diálogo)

Arquivo simples contendo termos separados por `;` ou `,` (pode ter 1 termo por linha). Exemplo:

```text
esofago;esophagus
```

### 3) `ExclusionTerms.csv` (opcional, automático)

O script tenta ler `ExclusionTerms.csv` na pasta do executável. Se existir, termos presentes aqui serão usados para **excluir** estruturas que contenham esses padrões.

### 4) `settings.json` (obrigatório)

O arquivo `settings.json` deve estar na pasta do executável (ou conforme o `SettingsLoader`). Ele define:

- `DthInBins` (bins do DTH IN)
- `DthOutBins` (bins do DTH OUT)
- `PointsInsideOarAxialResMm` (resolução axial da amostragem em `mm`)
- `DvhResolution` (resolução do DVH em `%`)
- `DvhMaxDose` (dose máxima para DVH em `%`)

---

## Saídas

A solução escreve 3 arquivos CSV na pasta de saída (padrão: pasta do executável). O prefixo é o **primeiro termo** do arquivo de busca do OAR.

### 1) `<prefix>_dths.csv`

Arquivo de *features* geométricas/DTH **sem identificadores do paciente**.

Cada linha do arquivo corresponde a um planejamento, ou seja, uma linha por caso_id.

**Colunas:**

- `caso_id`
- `volume_alvo`
- `dthIn_*` (uma coluna por bin)
- `dthOut_*` (uma coluna por bin)

### 2) `<prefix>_dvhs.csv`

Arquivo com valores **dose×volume** do DVH (cumulativo) do OAR.

Diferentemente do arquivo de DTH, **cada caso_id contribui com múltiplas linhas**, sendo **uma linha para cada bin do DVH**.

Esses bins são definidos no arquivo settings.json através dos parâmetros:

- `DvhResolution` (resolução do DVH)
- `DvhMaxDose` (dose máxima considerada no DVH)


**Colunas:**

- `caso_id`
- `dose_perc`
- `volume_perc`

### 3) `plans_identifier.csv`

Arquivo separado para **relacionar** o `caso_id` aos identificadores do Eclipse **(apenas para rastreabilidade)**.

**Colunas:**

- `caso_id`
- `patient_id`
- `course_id`
- `plan`

---

## Principais componentes (arquitetura rápida)

- **UseCases**
  - `ExtractionRunner`:
    - Orquestra leitura do CSV de entrada
    - Abre paciente (ESAPI)
    - Resolve `Course`, `PlanSetup`, `PTV` e `OAR`
    - Calcula DTH e extrai DVH
    - Gera CSVs

- **Domain**
  - `DistanceToTargetHistogram`:
    - Cria o histograma a partir das distâncias e bins
  - `PointsInsideOar`:
    - Amostra pontos no OAR e separa em IN/OUT
  - `OarToPtvDistances`:
    - Calcula distâncias entre pontos do OAR e o PTV
  - `DVHMetrics`:
    - Extrai os pontos do DVH (cumulativo)

- **Infrastructure**
  - `CsvHeaderBuilder`:
    - Monta cabeçalho do `*_dths.csv` com `caso_id`, `volume_alvo`, `dthIn_*`, `dthOut_*`
  - `SettingsLoader`:
    - Carrega e valida `settings.json`
  - `Logger`:
    - Logs no console

---

## Execução (passo a passo)

1. Compile a solução no Visual Studio (ambiente com ESAPI configurado).
2. Garanta que `settings.json` esteja acessível conforme o `SettingsLoader`.
3. Execute o script/aplicação.
4. Selecione:
   - CSV de casos
   - CSV de termos de busca do OAR
5. Ao final, serão gerados:
   - `<prefix>_dths.csv`
   - `<prefix>_dvhs.csv`
   - `<prefix>_plans_identifier.csv`

---

## Observações

- **Separador decimal:** o script força `CultureInfo.InvariantCulture` para evitar problemas com `,`/`.`.
- **Separador do CSV de entrada:** o parser aceita tanto `;` quanto `,` como separadores de coluna.
- **Case ID:** `caso_id` é um inteiro sequencial (1,2,3...) atribuído na ordem de processamento do CSV.
- **Robustez:** se paciente/curso/plano/PTV/OAR não forem encontrados, o caso é ignorado com log apropriado.
