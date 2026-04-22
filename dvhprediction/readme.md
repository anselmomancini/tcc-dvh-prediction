# Pipeline de Predição de DVH com Análise Fatorial e XGBoost

## Descrição

Este projeto implementa um pipeline de modelagem preditiva para curvas DVH (*Dose Volume Histogram*) em radioterapia.

O script utiliza descritores geométricos baseados em DTH (*Distance-To-Target Histogram*) para prever o comportamento da curva DVH de um órgão utilizando XGBoost.

O pipeline inclui:

- estatística descritiva
- análise de correlação
- redução de dimensionalidade via análise fatorial
- normalização de variáveis
- treinamento de modelo XGBoost
- validação cruzada por grupo
- avaliação global e por faixa de dose
- visualização de DVH real vs. predita
- interpretação do modelo com SHAP

---

# Estrutura do Pipeline

O script segue as seguintes etapas, respeitando a numeração das células do arquivo `dvh_prediction.py`:

1. Bibliotecas  
2. Configurações  
3. Funções auxiliares  
4. Leitura dos dados  
5. Estatística descritiva  
6. Correlação  
7. Separação treino-teste  
8. Seleção de variáveis  
9. Análise fatorial  
10. Montagem e normalização  
11. Base final  
12. XGBoost com busca aleatória  
13. Avaliação  
14. Salvamento dos artefatos  
15. Curva DVH real vs. predita  
16. Exemplo de curva  
17. Explicabilidade (SHAP)  
18. Exemplo de explicabilidade  

---

# Estrutura de diretórios

O script assume a seguinte organização:

```text
project/
├── inputs/
│   ├── <orgao>_dths.csv
│   └── <orgao>_dvhs.csv
└── outputs/
```

O diretório de saída é criado automaticamente pelo script.

---

# Arquivos de entrada

## DTHs

Arquivo esperado:

`inputs/<orgao>_dths.csv`

Contém:

- `caso_id`
- `volume_alvo`
- variáveis com prefixo `dthIn_*`
- variáveis com prefixo `dthOut_*`

As colunas `dthIn_*` e `dthOut_*` são filtradas para remover variáveis com variância nula antes da análise fatorial.

---

## DVHs

Arquivo esperado:

`inputs/<orgao>_dvhs.csv`

Contém:

- `caso_id`
- `dose_perc`
- `volume_perc`

Em DVHs, cada `caso_id` aparece em múltiplas linhas, representando diferentes pontos da curva ao longo da dose.

---

# Parâmetros do modelo

Configuração global:

```python
CFG = {"seed": 42, "test_size": 0.20, "cv_splits": 5}
```

Significado:

`seed`  
controle de reprodutibilidade

`test_size`  
proporção do conjunto de teste

`cv_splits`  
número máximo de folds da validação cruzada em grupo

---

# Órgão utilizado

No código, a variável é definida como:

```python
ORGAO = "arvore_bronquica"
```

---

# Redução de dimensionalidade

A redução de dimensionalidade é realizada utilizando análise fatorial com método principal.

Separadamente para:

- `dthIn_*`
- `dthOut_*`

Número de fatores utilizados no código:

- `dthIn_*` → 3 fatores, com rotação `varimax`
- `dthOut_*` → 1 fator, sem rotação

As componentes derivadas de `dthIn_*` são renomeadas automaticamente como:

- `axial_adjacente`
- `axial_media`
- `axial_periferica`

Com base na distância dominante das variáveis de entrada.

A componente derivada de `dthOut_*` é:

- `long_adjacente`

---

# Variáveis utilizadas no modelo

As variáveis preditoras finais são:

- `volume_alvo`
- `axial_adjacente`
- `axial_media`
- `axial_periferica`
- `long_adjacente`
- `dose_perc`

A variável alvo é:

- `volume_perc`

---

# Modelo de Machine Learning

O modelo utilizado é:

**XGBoost Regressor**

Configuração base:

```python
XGBRegressor(
    objective="reg:squarederror",
    colsample_bytree=1,
    n_jobs=-1,
    random_state=42
)
```

---

# Otimização de hiperparâmetros

A busca é realizada com `RandomizedSearchCV`.

Número de combinações testadas:

- `n_iter = 250`

Hiperparâmetros otimizados:

- `n_estimators`
- `max_depth`
- `learning_rate`
- `subsample`
- `reg_alpha`
- `reg_lambda`
- `min_child_weight`
- `gamma`

A métrica usada na busca é:

- `R²`

---

# Validação

A separação treino-teste é feita antes da junção com os DVHs, mantendo cada `caso_id` em apenas um conjunto.

A validação cruzada utiliza:

- `GroupKFold`

Agrupamento por:

- `caso_id`

Isso evita vazamento de informação entre múltiplos pontos DVH do mesmo caso.

---

# Métricas de avaliação

O script calcula:

- `R²`
- `MAE`
- `RMSE`

São reportados:

- métricas globais para treino
- métricas globais para teste
- média do `R²` em validação cruzada
- desvio-padrão do `R²` em validação cruzada
- desempenho por faixa de dose no conjunto de teste

As faixas de dose definidas no pipeline são:

- `0-20`
- `>20-40`
- `>40-60`
- `>60-80`
- `>80-105`

---

# Artefatos salvos

Após o treinamento, os seguintes objetos são armazenados em `outputs/<orgao>/` com timestamp:

- `<orgao>_scaler_*.joblib`
- `<orgao>_xgb_*.joblib`
- `<orgao>_fa_in_*.joblib`
- `<orgao>_fa_out_*.joblib`
- `<orgao>_metricas_globais_*.csv`
- `<orgao>_metricas_faixa_teste_*.csv`
- `<orgao>_metadata_*.json`

O arquivo de metadados registra, entre outros itens:

- órgão analisado
- seed
- timestamp
- colunas usadas no modelo
- variável alvo
- melhores hiperparâmetros
- mapeamento dos fatores renomeados

---

# Visualização de DVH

O script define a função `plot_dvh_pred()`, que gera um gráfico comparando:

- DVH real
- DVH predita pelo modelo

Eixos do gráfico:

**Eixo X**  
Dose (% da prescrição)

**Eixo Y**  
Volume (%)

No exemplo do código, a função é aplicada ao:

- `caso_id = 173`

---

# Interpretação com SHAP

O modelo é interpretado usando SHAP.

A função `shap_pair_plot()` gera dois painéis:

1. **Distribuição das explicativas**  
   Mostra a distribuição das variáveis nos dados de treino para observações com a mesma dose, incluindo a coloração pelos valores SHAP e o círculo preto do caso analisado.

3. **Waterfall plot**  
   Mostra a contribuição individual de cada variável para a predição do modelo.

No exemplo do código, a explicação é gerada para:

- `caso_id = 173`
- `dose_perc = 10`

usando como baseline os dados do treino com a mesma dose.

---

# Reprodutibilidade

O script define:

```python
np.random.seed(42)
```

Além disso, a mesma seed é utilizada na separação treino-teste, no modelo XGBoost e na busca aleatória de hiperparâmetros.

---
