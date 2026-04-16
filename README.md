# Aplicação de machine learning explicável para predição de dose em órgãos adjacentes na radioterapia pulmonar

Este repositório reúne os códigos desenvolvidos no Trabalho de Conclusão de Curso em Data Science e Analytics voltado à predição de curvas DVH (*Dose-Volume Histogram*) de órgãos adjacentes ao tumor em radioterapia pulmonar com técnica SBRT/VMAT.

O projeto foi estruturado em três módulos complementares:

- `doseprofiles`: extração, a partir do sistema de planejamento Eclipse, de perfis de dose utilizados na definição qualitativa dos intervalos dos DTHs;
- `dataextractor`: extração, a partir do sistema de planejamento Eclipse, de variáveis geométricas e dosimétricas dos casos selecionados;
- `dvhprediction`: pipeline de modelagem preditiva em Python com análise fatorial, XGBoost e SHAP.

## Objetivo

Desenvolver um pipeline de machine learning explicável para estimar curvas DVH de órgãos adjacentes ao alvo em pacientes submetidos à radioterapia pulmonar, sintetizando a relação espacial entre alvo e órgãos e acrescentando transparência às predições do modelo.

## Estrutura do repositório

```text
tcc-dvh-prediction/
├── README.md
├── CITATION.cff
├── .gitignore
├── requirements.txt
├── doseprofiles/
├── dataextractor/
└── dvhprediction/
```

## Visão geral dos módulos

### `doseprofiles`

Aplicação em C# com ESAPI para extração de perfis de dose axial e longitudinal a partir da borda do alvo. Esses perfis foram utilizados como referência qualitativa para definição dos intervalos adotados na discretização dos histogramas DTH.

### `dataextractor`

Aplicação em C# com ESAPI para extração automática das variáveis do estudo, incluindo:

- volume do alvo;
- histogramas DTH-In e DTH-Out;
- pontos da curva DVH cumulativa.

As saídas são organizadas em arquivos CSV estruturados para uso posterior na modelagem.

### `dvhprediction`

Pipeline em Python para:

- estatística descritiva;
- análise de correlação;
- redução de dimensionalidade por análise fatorial;
- normalização das variáveis;
- treinamento com XGBoost;
- validação cruzada por grupo;
- avaliação global e por faixa de dose;
- comparação entre DVHs reais e preditas;
- interpretabilidade local com SHAP.

## Metodologia resumida

O pipeline foi desenvolvido para dados de pacientes tratados com SBRT pulmonar com técnica VMAT.

As variáveis explicativas iniciais incluem descritores baseados em *Distance-to-Target Histogram* (DTH), subdividido em duas componentes:

- **DTH-In**: fração do órgão presente nos mesmos planos axiais do alvo;
- **DTH-Out**: fração do órgão presente em planos axiais que não contêm o alvo.

As variáveis DTH foram submetidas à redução de dimensionalidade por análise fatorial. Os constructos obtidos foram combinados ao volume do alvo e à variável `dose_perc`, compondo a entrada do modelo de regressão com XGBoost. A interpretabilidade das estimativas foi analisada com SHAP.

## Base de dados e escopo do estudo

O estudo foi conduzido com dados retrospectivos de pacientes tratados com SBRT pulmonar, técnica VMAT.

Foram analisados quatro órgãos adjacentes ao alvo:

- árvore brônquica;
- coração;
- esôfago;
- medula espinhal.

## Fluxo geral do projeto

De forma resumida, o projeto seguiu as etapas abaixo:

1. extração de perfis médios de dose em C# via ESAPI;
2. definição qualitativa dos intervalos dos DTHs;
3. extração automática das variáveis geométricas e dosimétricas;
4. organização dos dados em arquivos CSV;
5. separação treino-teste por caso;
6. redução de dimensionalidade por análise fatorial;
7. normalização das variáveis explicativas;
8. treinamento e ajuste do modelo XGBoost;
9. avaliação global e estratificada por faixa de dose;
10. análise de interpretabilidade local com SHAP.

## Dados disponibilizados

Este repositório disponibiliza apenas os arquivos utilizados no exemplo da **árvore brônquica**, presentes em:

```text
dvhprediction/inputs/
```

## Tecnologias utilizadas

### `doseprofiles` e `dataextractor`

- C#
- .NET Framework 4.6.2
- Varian Eclipse
- ESAPI 16.1

### `dvhprediction`

- Python 3
- NumPy
- pandas
- matplotlib
- seaborn
- scikit-learn
- XGBoost
- SHAP
- factor-analyzer
- joblib

## Instalação do ambiente Python

```bash
pip install -r requirements.txt
```

## Execução

A execução do pipeline preditivo é realizada no módulo `dvhprediction`, utilizando os arquivos de entrada disponíveis na pasta `inputs/`.

Exemplo:

```bash
python dvh_prediction.py
```

> Observação: os módulos `doseprofiles` e `dataextractor` dependem de ambiente institucional autorizado, com acesso ao Eclipse/ESAPI.

## Saídas do pipeline

O pipeline em Python foi desenvolvido para gerar, entre outros artefatos:

- métricas globais de desempenho;
- métricas por faixa de dose;
- modelos ajustados;
- escalonador;
- objetos da análise fatorial;
- metadados da execução;
- gráficos de comparação entre DVHs reais e preditas;
- gráficos de interpretabilidade com SHAP.

## Uso e citação

Este repositório é disponibilizado publicamente para fins acadêmicos e apresentação metodológica.

Se você for referenciar este material, utilize as informações disponíveis em `CITATION.cff`.

Nenhuma licença open source está sendo concedida neste momento para reutilização, modificação ou redistribuição do código.

## Observações

- Os dados disponibilizados são limitados ao necessário para ilustrar o funcionamento do pipeline de predição de dvh.

## Autor

**Anselmo Mancini**

Universidade de São Paulo – USP/ESALQ
