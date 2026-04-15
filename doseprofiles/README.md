# DoseProfiles

## Visão Geral

DoseProfiles é uma aplicação **ESAPI (Eclipse Scripting API)**
desenvolvida em **C# (.NET Framework 4.6.2)** para extração automática
de **perfis de dose** a partir de planos de tratamento radioterápico no
Eclipse.

A aplicação percorre uma lista de pacientes definida em um arquivo CSV
e, para cada plano especificado, extrai dois tipos de perfis de dose:

-   **Perfil Axial (horizontal)** --- ao longo do eixo X
-   **Perfil Longitudinal** --- ao longo do eixo Z

Os perfis são extraídos a partir da **superfície do PTV**, seguindo uma
direção definida em relação ao centro do corpo do paciente, e são
exportados em formato **CSV**, possibilitando análise posterior.

------------------------------------------------------------------------

# Requisitos

-   **Varian Eclipse**
-   **ESAPI 16.1**
-   **.NET Framework 4.6.2**
-   Acesso à base de pacientes via ESAPI
-   Plano com **dose calculada**

Referências principais do ESAPI utilizadas:

    VMS.TPS.Common.Model.API
    VMS.TPS.Common.Model.Types

------------------------------------------------------------------------

# Estrutura da Solução

A solução está organizada em camadas para separar responsabilidades:

    DoseProfiles
    │
    ├── Domain
    │   ├── Models
    │   │   ├── PatientRow.cs
    │   │   ├── PatientProfile.cs
    │   │   └── PatientProfiles.cs
    │   │
    │   └── Logging
    │       └── ILogger.cs
    │
    ├── Infrastructure
    │   ├── EsapiDoseProfileBuilder.cs
    │   ├── CsvProfilesWriter.cs
    │   ├── CsvLogger.cs
    │   └── CsvUtil.cs
    │
    ├── UseCases
    │   └── ProfilesExtractionRunner.cs
    │
    └── Presentation
        └── Program.cs

### Domain

Define os modelos de dados e contratos utilizados pela aplicação.

### Infrastructure

Contém implementações que interagem com sistemas externos:

-   ESAPI
-   Arquivos CSV
-   Sistema de log

### UseCases

Orquestra o fluxo principal da aplicação para extração de perfis.

### Presentation

Contém o ponto de entrada da aplicação (`Main`).

------------------------------------------------------------------------

# Arquivo de Entrada

A aplicação lê um arquivo chamado:

    patients.csv

Localizado na **mesma pasta do executável**.

Formato esperado:

    patient_id,course_id,plan_id,ptv_id

Exemplo:

    123456,Course1,Plan1,PTV
    789012,Course1,PlanA,PTV_70

Campos:

  Campo        Descrição
  ------------ -----------------------------------------
  patient_id   ID do paciente no Eclipse
  course_id    Curso do tratamento
  plan_id      Plano de tratamento
  ptv_id       Estrutura PTV utilizada como referência

------------------------------------------------------------------------

# Funcionamento Geral

O fluxo da aplicação:

1.  Inicialização do ESAPI (`Application.CreateApplication()`)
2.  Leitura do arquivo `patients.csv`
3.  Para cada linha:
    -   Abrir paciente
    -   Localizar Course
    -   Localizar PlanSetup
    -   Localizar StructureSet
    -   Identificar PTV
    -   Identificar EXTERNAL (corpo)
4.  Determinar o **centro do PTV**
5.  Determinar a **superfície do PTV** ao longo do eixo de interesse
6.  Construir um segmento entre:

```{=html}
<!-- -->
```
    surface_ptv → direção escolhida → comprimento do perfil

7.  Amostrar o perfil de dose usando:

```{=html}
<!-- -->
```
    plan.Dose.GetDoseProfile(start, end, samples)

8.  Converter valores de dose para **percentual**
9.  Exportar resultados em arquivos CSV

------------------------------------------------------------------------

# Perfis de Dose Extraídos

## Perfil Axial (Horizontal)

Características:

-   Eixo: **X**
-   Origem: superfície do PTV
-   Direção: definida pelo centro do corpo (`EXTERNAL`)
-   Plano de amostragem: centro do PTV em **Y e Z**

------------------------------------------------------------------------

## Perfil Longitudinal

Características:

-   Eixo: **Z**
-   Origem: superfície do PTV
-   Direção: positiva no eixo Z
-   Plano de amostragem: centro do PTV em **X e Y**

------------------------------------------------------------------------

# Amostragem do Perfil

O perfil é discretizado em pontos igualmente espaçados.

Parâmetros:

  Parâmetro   Descrição
  ----------- -------------------------
  lengthMm    comprimento do perfil
  stepMm      resolução de amostragem
  samples     número de pontos

Cálculo típico:

    samples = floor(length / step) + 1

------------------------------------------------------------------------

# Normalização da Dose

Os valores retornados pelo ESAPI podem estar em:

-   **Percentual**
-   **Dose absoluta**

A aplicação garante que todos os valores exportados estejam em **% da
dose total do plano**.


------------------------------------------------------------------------

# Arquivos de Saída

A aplicação gera três arquivos:

    perfis_axial_horizontal.csv
    perfis_longitudinal.csv
    doseprofiles_log.csv

------------------------------------------------------------------------

## perfis_axial_horizontal.csv

Perfis extraídos ao longo do eixo **X**.

Estrutura:

    patient_id,course_id,plan_id,ptv_id,length_mm,step_mm,n_points,
    dose_pct_point_0,dose_pct_point_1,...,dose_pct_point_N

------------------------------------------------------------------------

## perfis_longitudinal.csv

Perfis extraídos ao longo do eixo **Z**.

Mesma estrutura do arquivo axial.

------------------------------------------------------------------------

# Log da Execução

Arquivo:

    doseprofiles_log.csv

Formato:

    timestamp,level,patient_id,course_id,plan_id,ptv_id,message,exception

Níveis de log:

  Level   Descrição
  ------- ---------------------------------
  INFO    Evento informativo
  WARN    Condição inesperada não crítica
  ERROR   Falha durante processamento

Cada paciente é processado de forma independente.\
Falhas não interrompem o processamento dos demais.

------------------------------------------------------------------------

# Execução

1.  Compilar a solução no Visual Studio
2.  Copiar `patients.csv` para a pasta do executável
3.  Executar o programa

Arquivos de saída serão gerados na mesma pasta.


