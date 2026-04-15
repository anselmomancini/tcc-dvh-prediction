# -*- coding: utf-8 -*-
#%% [1] Bibliotecas
# -------------------------------------------------------------------------------------------
from pathlib import Path
from datetime import datetime
import json, joblib
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import seaborn as sns

from factor_analyzer import FactorAnalyzer, calculate_bartlett_sphericity
from sklearn.model_selection import train_test_split, GroupKFold, RandomizedSearchCV
from sklearn.preprocessing import MinMaxScaler
from sklearn.metrics import r2_score, mean_absolute_error, mean_squared_error
from xgboost import XGBRegressor

#%% [2] Configurações
# -------------------------------------------------------------------------------------------
CFG = {"seed": 42, "test_size": 0.20, "cv_splits": 5}

ORGAO = "arvore_bronquica"  # arvore_bronquica | coracao | esofago | medula

PC_IN_NAMES = ["axial_adjacente", "axial_media", "axial_periferica"]
PC_OUT_NAMES = ["long_adjacente"]
FEATURES = ["volume_alvo"] + PC_IN_NAMES + PC_OUT_NAMES

DOSE_BINS = [0, 20, 40, 60, 80, 105]
DOSE_LABELS = ["0-20", ">20-40", ">40-60", ">60-80", ">80-105"]

IN_DIR = Path("inputs")
OUT_DIR = Path("outputs") / ORGAO
OUT_DIR.mkdir(parents=True, exist_ok=True)

np.random.seed(CFG["seed"])
plt.rcParams.update({"figure.dpi": 110, "savefig.dpi": 110, "font.size": 10})

#%% [3] Funções auxiliares
# -------------------------------------------------------------------------------------------
def cols_by_prefix(df, prefix):
    """Seleciona colunas com prefixo e variância não nula."""
    return [c for c in df.columns if c.startswith(prefix) and df[c].var() != 0]

def plot_corr(df, cols, title, figsize):
    """Plota matriz de correlação."""
    if not cols:
        return
    fig, ax = plt.subplots(figsize=figsize)
    sns.heatmap(df[cols].corr(), cmap="viridis", annot=True, fmt=".3f",
                square=True, cbar=True, annot_kws={"size": 7}, ax=ax)
    ax.set_title(title)
    ax.grid(False)
    plt.tight_layout()
    plt.show()

def plot_loadings(load, title):
    """Plota cargas fatoriais."""
    x = np.arange(load.shape[1])
    n_vars = load.shape[0]
    bar_w = 0.9 / n_vars
    colors = plt.get_cmap("viridis")(np.linspace(0, 1, n_vars))

    fig, ax = plt.subplots(figsize=(12, 6))
    for i, (var, col) in enumerate(zip(load.index, colors)):
        ax.bar(x - 0.45 + (i + 0.5) * bar_w, load.loc[var],
               width=bar_w * 0.98, color=col)

    ax.set_xticks(x)
    ax.set_xticklabels(load.columns)
    ax.set_ylabel("Carga fatorial")
    ax.set_title(title)
    ax.grid(True, alpha=0.25)
    ax.legend(load.index, title="Variáveis", loc="center left", bbox_to_anchor=(1.02, 0.5))
    plt.tight_layout()
    plt.show()

def fit_fa(X, n_factors, rotation=None, title=""):
    """Ajusta a FA, calcula autovalores e retorna auditoria."""
    _, p = calculate_bartlett_sphericity(X)

    fa0 = FactorAnalyzer(
        n_factors=X.shape[1], method="principal", rotation=None
    ).fit(X)
    ev, _ = fa0.get_eigenvalues()
    ev = np.asarray(ev, float)

    fa = FactorAnalyzer(
        n_factors=n_factors, method="principal", rotation=rotation
    ).fit(X)

    load = pd.DataFrame(
        fa.loadings_,
        index=X.columns,
        columns=[f"PC{i+1}" for i in range(n_factors)]
    )
    plot_loadings(load, title)

    audit = {
        "bartlett_p": float(p),
        "n_kaiser": int((ev > 1).sum()),
        "cumvar_used": float(np.cumsum(ev)[n_factors - 1] / ev.sum()) if ev.sum() > 0 else np.nan
    }
    return fa, load, audit

def fa_scores(fa, X, rename=None):
    """Calcula scores fatoriais."""
    S = pd.DataFrame(
        fa.transform(X),
        index=X.index,
        columns=[f"PC{i+1}" for i in range(fa.n_factors)]
    )
    return S.rename(columns=rename or {})

def dist_from_col(col):
    """Extrai a distância numérica do nome da variável."""
    try:
        return float(col.split("_", 1)[1])
    except Exception:
        return np.nan

def rename_dthin(load):
    """Renomeia fatores do DTH-In segundo a distância dominante."""
    d = np.array([dist_from_col(c) for c in load.index], dtype=float)
    valid = ~np.isnan(d)
    cols, d = load.index[valid], d[valid]

    masks = {
        PC_IN_NAMES[0]: d <= 20,
        PC_IN_NAMES[1]: (d > 20) & (d <= 60),
        PC_IN_NAMES[2]: d > 60
    }

    mapping, used = {}, set()
    for pc in load.columns:
        L = load.loc[cols, pc].abs().values
        means = {k: L[m].mean() if m.any() else -np.inf for k, m in masks.items()}
        for name, _ in sorted(means.items(), key=lambda x: x[1], reverse=True):
            if name not in used:
                mapping[pc] = name
                used.add(name)
                break
    return mapping

def assemble(base, s_in, s_out):
    """Consolida volume do alvo e scores fatoriais."""
    return (
        base[["caso_id", "volume_alvo"]]
        .join(s_in.reindex(columns=PC_IN_NAMES, fill_value=0.0))
        .join(s_out.reindex(columns=PC_OUT_NAMES, fill_value=0.0))
    )

def compute_metrics(y_true, y_pred):
    """Calcula R2, MAE e RMSE."""
    y_true = np.asarray(y_true, dtype=float)
    y_pred = np.asarray(y_pred, dtype=float)
    return {
        "R2": r2_score(y_true, y_pred)
              if (len(y_true) >= 2 and not np.isclose(np.var(y_true), 0))
              else np.nan,
        "MAE": mean_absolute_error(y_true, y_pred) if len(y_true) else np.nan,
        "RMSE": np.sqrt(mean_squared_error(y_true, y_pred)) if len(y_true) else np.nan
    }

def metrics_by_dose_band(
    df,
    y_true_col="volume_perc",
    y_pred_col="y_pred",
    dose_col="dose_perc"
):
    dose_bins = [0, 20, 40, 60, 80, 105]
    dose_labels = ["0–20", "20–40", "40–60", "60–80", "80–105"]

    df_eval = df.copy()
    df_eval["faixa_dose"] = pd.cut(
        df_eval[dose_col],
        bins=dose_bins,
        labels=dose_labels,
        include_lowest=True,
        right=False
    )

    resultados = []

    for faixa in dose_labels:
        sub = df_eval[df_eval["faixa_dose"] == faixa]
        if sub.empty:
            continue

        y_true = sub[y_true_col]
        y_pred = sub[y_pred_col]
        metrics = compute_metrics(y_true, y_pred)

        resultados.append({
            "faixa_dose": faixa,
            "n": int(len(sub)),
            "variancia": float(y_true.var(ddof=1)),
            **metrics
        })

    return pd.DataFrame(resultados)

#%% [4] Leitura dos dados
# -------------------------------------------------------------------------------------------
dths = pd.read_csv(IN_DIR / f"{ORGAO}_dths.csv")
dvhs = pd.read_csv(IN_DIR / f"{ORGAO}_dvhs.csv")

#%% [5] Estatística descritiva
# -------------------------------------------------------------------------------------------
print("\nEstatística descritiva - DTHs")
print(dths.drop(columns="caso_id", errors="ignore").describe().T)

dvhs_desc = dvhs.copy()
dvhs_desc["faixa_dose"] = pd.cut(
    dvhs_desc["dose_perc"],
    bins=DOSE_BINS,
    labels=DOSE_LABELS,
    include_lowest=True,
    right=True
)

print("\nEstatística descritiva - DVHs por faixa de dose")
print(
    dvhs_desc.groupby("faixa_dose", observed=False)["volume_perc"]
    .agg(
        count="count",
        mean="mean",
        std="std",
        min="min",
        q25=lambda x: x.quantile(0.25),
        q50=lambda x: x.quantile(0.50),
        q75=lambda x: x.quantile(0.75),
        max="max",
    )
)

#%% [6] Correlação
# -------------------------------------------------------------------------------------------
cols_in_corr = cols_by_prefix(dths, "dthIn_")
cols_out_corr = cols_by_prefix(dths, "dthOut_")

plot_corr(dths, cols_in_corr, f"Matriz de Correlação – dthIn_* [{ORGAO}]", (12, 10))
plot_corr(dths, cols_out_corr, f"Matriz de Correlação – dthOut_* [{ORGAO}]", (8, 6))

#%% [7] Separação treino-teste
# -------------------------------------------------------------------------------------------
# A separação é feita antes da junção com os DVHs, mantendo cada caso_id em um único conjunto.
tr, te = train_test_split(
    dths, test_size=CFG["test_size"], random_state=CFG["seed"]
)

#%% [8] Seleção de variáveis
# -------------------------------------------------------------------------------------------
cols_in = cols_by_prefix(tr, "dthIn_")
cols_out = cols_by_prefix(tr, "dthOut_")

#%% [9] Análise fatorial
# -------------------------------------------------------------------------------------------
# Ajuste da FA no treino e aplicação no treino/teste, evitando vazamento de dados.
fa_in, L_in, aud_in = fit_fa(
    tr[cols_in], n_factors=3, rotation="varimax",
    title=f"Loadings dthIn [{ORGAO}]"
)
fa_out, L_out, aud_out = fit_fa(
    tr[cols_out], n_factors=1, rotation=None,
    title=f"Loadings dthOut [{ORGAO}]"
)

rename_in = rename_dthin(L_in)

S_in_tr = fa_scores(fa_in, tr[cols_in], rename_in)
S_out_tr = fa_scores(fa_out, tr[cols_out], {"PC1": PC_OUT_NAMES[0]})
S_in_te = fa_scores(fa_in, te[cols_in], rename_in)
S_out_te = fa_scores(fa_out, te[cols_out], {"PC1": PC_OUT_NAMES[0]})

print(f"[FA:{ORGAO}] dthIn  | Bartlett p={aud_in['bartlett_p']:.4g} | "
      f"Kaiser={aud_in['n_kaiser']} | var_acum(3)={100 * aud_in['cumvar_used']:.2f}%")
print(f"[FA:{ORGAO}] dthOut | Bartlett p={aud_out['bartlett_p']:.4g} | "
      f"Kaiser={aud_out['n_kaiser']} | var_acum(1)={100 * aud_out['cumvar_used']:.2f}%")

#%% [10] Montagem e normalização
# -------------------------------------------------------------------------------------------
pc_tr = assemble(tr, S_in_tr, S_out_tr)
pc_te = assemble(te, S_in_te, S_out_te)

scaler = MinMaxScaler()
# Fit e transform no treino; apenas transform no teste.
pc_tr[FEATURES] = scaler.fit_transform(pc_tr[FEATURES])
pc_te[FEATURES] = scaler.transform(pc_te[FEATURES])

#%% [11] Base final
# -------------------------------------------------------------------------------------------
# A junção com os DVHs expande os casos em múltiplas linhas, mas preserva a separação treino/teste.
train_long = pc_tr.merge(dvhs, on="caso_id")
test_long = pc_te.merge(dvhs, on="caso_id")

X_cols = FEATURES + ["dose_perc"]
y_col = "volume_perc"

X_train, y_train = train_long[X_cols], train_long[y_col]
X_test, y_test = test_long[X_cols], test_long[y_col]

groups_train = train_long["caso_id"]
cv = GroupKFold(n_splits=min(CFG["cv_splits"], max(2, groups_train.nunique())))

#%% [12] XGBoost com busca aleatória
# -------------------------------------------------------------------------------------------
xgb = XGBRegressor(
    objective="reg:squarederror",
    colsample_bytree=1,
    n_jobs=-1,
    random_state=CFG["seed"]
)

param_dist = {
    "n_estimators": [250, 500, 750, 1000, 1250],
    "max_depth": [2, 3, 4, 5],
    "learning_rate": [0.01, 0.02, 0.03, 0.04, 0.05],
    "subsample": [0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0],
    "reg_alpha": [0, 0.5, 1, 2, 4, 8, 12],
    "reg_lambda": [1, 3, 5, 7, 10],
    "min_child_weight": [1, 3, 5, 7, 10],
    "gamma": [0, 0.5, 1, 2, 5],
}

search = RandomizedSearchCV(
    estimator=xgb,
    param_distributions=param_dist,
    n_iter=250,
    cv=cv,
    n_jobs=-1,
    scoring="r2",
    random_state=CFG["seed"]
)

search.fit(X_train, y_train, groups=groups_train)
model = search.best_estimator_

#%% [13] Avaliação
# -------------------------------------------------------------------------------------------
y_pred_train = model.predict(X_train)
y_pred_test = model.predict(X_test)

# Métricas globais em treino e teste
metrics_train = compute_metrics(y_train, y_pred_train)
metrics_test = compute_metrics(y_test, y_pred_test)

global_metrics = pd.DataFrame([
    {
        "orgao": ORGAO,
        "conjunto": "treino",
        "n": int(len(y_train)),
        "variancia": float(y_train.var(ddof=1)),
        **metrics_train
    },
    {
        "orgao": ORGAO,
        "conjunto": "teste",
        "n": int(len(y_test)),
        "variancia": float(y_test.var(ddof=1)),
        **metrics_test
    }
])

# R2 da validação cruzada do melhor modelo selecionado no RandomizedSearchCV
cv_mean = float(search.best_score_)
cv_std = float(search.cv_results_["std_test_score"][search.best_index_])

# Desempenho por faixa de dose no conjunto de teste
dose_metrics_test = metrics_by_dose_band(
    test_long.assign(y_pred=y_pred_test),
    y_true_col=y_col,
    y_pred_col="y_pred",
    dose_col="dose_perc"
)

# Impressão formatada
print("\nMétricas globais:")
print(global_metrics.to_string(index=False, float_format=lambda x: f"{x:.4f}"))

print(f"R2 CV={cv_mean:.4f}±{cv_std:.4f}")

print("\nDesempenho por faixa de dose (teste):")
print(dose_metrics_test.to_string(index=False, float_format=lambda x: f"{x:.4f}"))

print("\nBest params:", search.best_params_)

#%% [14] Salvamento dos artefatos
# -------------------------------------------------------------------------------------------
# Persistência dos objetos principais do pipeline para reprodutibilidade.
stamp = datetime.now().strftime("%Y%m%d-%H%M%S")

artefatos = {
    "scaler": scaler,
    "xgb": model,
    "fa_in": fa_in,
    "fa_out": fa_out,
}

for nome, obj in artefatos.items():
    joblib.dump(obj, OUT_DIR / f"{ORGAO}_{nome}_{stamp}.joblib")

global_metrics.to_csv(OUT_DIR / f"{ORGAO}_metricas_globais_{stamp}.csv", index=False)
dose_metrics_test.to_csv(OUT_DIR / f"{ORGAO}_metricas_faixa_teste_{stamp}.csv", index=False)

meta = {
    "organ": ORGAO,
    "seed": CFG["seed"],
    "timestamp": stamp,
    "X_cols": X_cols,
    "y_col": y_col,
    "best_params": search.best_params_,
    "rename_in": rename_in,
    "rename_out": {"PC1": PC_OUT_NAMES[0]},
}

(OUT_DIR / f"{ORGAO}_metadata_{stamp}.json").write_text(
    json.dumps(meta, ensure_ascii=False, indent=2),
    encoding="utf-8"
)

print(f"\n[{ORGAO}] Artefatos e métricas salvos em: {OUT_DIR}")

#%% [15] Curva DVH real vs. predita
# -------------------------------------------------------------------------------------------
def plot_dvh_pred(caso_id, test_long, features, model):
    """Compara DVH real e predita para um caso do teste."""
    g = test_long.loc[test_long["caso_id"] == caso_id].sort_values("dose_perc")
    y = model.predict(g[[*features, "dose_perc"]])

    plt.figure(figsize=(8, 5))
    plt.plot(g["dose_perc"], g["volume_perc"], label="Real", color="tab:blue", linewidth=1.5)
    plt.plot(g["dose_perc"], y, label="XGBoost", color="tab:orange", linewidth=1.5)
    plt.title(f"DVH - caso_id {caso_id}")
    plt.xlabel("Dose (% da prescrição)")
    plt.ylabel("Volume (%)")
    plt.legend()
    plt.grid(True, alpha=0.3)
    plt.tight_layout()
    plt.show()

#%% [16] Exemplo de curva
# -------------------------------------------------------------------------------------------
# Caso pertencente exclusivamente ao conjunto de teste.
plot_dvh_pred(caso_id=119, test_long=test_long, features=FEATURES, model=model)

#%% [17] Explicabilidade (SHAP)
# -------------------------------------------------------------------------------------------
def shap_pair_plot(case_df, model, feature_cols, df_full):
    """Gera gráfico combinado de distribuição das explicativas e waterfall plot."""
    import shap
    from matplotlib.colors import TwoSlopeNorm

    if len(case_df) != 1:
        raise ValueError("case_df deve ter 1 linha")

    row = case_df.copy()
    dose = int(round(row["dose_perc"].iloc[0]))

    bg = df_full.loc[df_full["dose_perc"].round().astype(int) == dose]
    if bg.empty:
        raise ValueError(f"Sem baseline para dose={dose}")

    feats = [c for c in feature_cols if c != "dose_perc"]
    idx = {f: i for i, f in enumerate(feature_cols)}
    X_bg = bg[feature_cols]

    expl = shap.TreeExplainer(
        model,
        data=X_bg,
        feature_perturbation="interventional"
    )

    shap_bg = expl(X_bg, check_additivity=False).values
    sv = expl(row[feature_cols])
    vals = sv[0].values

    feats = sorted(feats, key=lambda f: abs(vals[idx[f]]), reverse=True)
    sel = [idx[f] for f in feats]

    v = np.nanmax(np.abs(shap_bg[:, sel]))
    norm = TwoSlopeNorm(vmin=-v, vcenter=0, vmax=v)

    fig, (ax_b, ax_w) = plt.subplots(1, 2, figsize=(14, 5), dpi=200)
    pos = np.arange(len(feats))[::-1]

    bp = ax_b.boxplot(
        [X_bg[f].values for f in feats],
        vert=False, positions=pos, showfliers=False,
        widths=0.6, patch_artist=True
    )
    for b in bp["boxes"]:
        b.set_alpha(.9)

    for j, f in enumerate(feats):
        k, y = idx[f], pos[j]
        x = X_bg[f].values

        ax_b.scatter(
            x, y + (np.random.rand(len(x)) - .5) * .18,
            c=shap_bg[:, k], cmap="coolwarm", norm=norm,
            s=18, alpha=.9, zorder=3
        )
        ax_b.scatter(
            float(row[f].iloc[0]), y,
            c=[vals[k]], cmap="coolwarm", norm=norm,
            s=55, edgecolor="black", zorder=5
        )

    ax_b.set(
        yticks=pos,
        yticklabels=feats,
        xlabel="Valor normalizado",
        title="Distribuição das explicativas"
    )

    cbar = fig.colorbar(
        plt.cm.ScalarMappable(norm=norm, cmap="coolwarm"),
        ax=ax_b, fraction=0.046, pad=0.02
    )
    cbar.set_label("Valor SHAP")

    plt.sca(ax_w)
    shap.plots.waterfall(
        shap.Explanation(
            values=vals[sel],
            base_values=sv.base_values[0],
            data=sv[0].data[sel] if sv[0].data is not None else None,
            feature_names=feats
        ),
        max_display=len(feats),
        show=False
    )

    ax_w.set_yticks([])
    pid = f" • caso_id={row['caso_id'].iloc[0]}" if "caso_id" in row else ""
    ax_w.set_title(f"Waterfall — dose={dose}%{pid}")

    plt.tight_layout()
    return fig

#%% [18] Exemplo de explicabilidade
# -------------------------------------------------------------------------------------------
case_df = test_long.loc[
    (test_long["caso_id"] == 119) &
    (test_long["dose_perc"].round().astype(int) == 10)
]

fig = shap_pair_plot(
    case_df=case_df,
    model=model,
    feature_cols=X_cols,
    df_full=train_long  # baseline condicionado à mesma dose e extraído do treino
)

plt.show()

