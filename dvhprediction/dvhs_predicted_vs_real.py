#%% [17] Visual — DVH: Reais vs. Preditos (grade por plano de teste)
# Ajustado para A4

import math
suptitle_size, axis_labels_size = 24, 24
subplot_title_size, legend_font_size, tick_label_size = 20, 27, 18

caso_ids_test = sorted(test_long["caso_id"].unique())
n_plans = len(caso_ids_test); n_cols = 7; n_rows = math.ceil(n_plans / n_cols)
fig, axes = plt.subplots(n_rows, n_cols, figsize=(n_cols * 4.5, n_rows * 4.4), sharex=True, sharey=True) # proporção da figura
axes = axes.flatten()

for i, caso_id in enumerate(caso_ids_test):
    ax = axes[i]
    g = test_long.loc[test_long["caso_id"] == caso_id].sort_values("dose_perc")
    dose_sorted, volume_real_sorted = g["dose_perc"], g["volume_perc"]
    Xg = g[[*FEATURES, "dose_perc"]]
    # volume_pred_xgb = np.clip(model.predict(Xg), 0, 100)
    volume_pred_xgb = model.predict(Xg) # sem clip
    

    ax.plot(dose_sorted, volume_real_sorted, label="Real", color="tab:blue", linewidth=5, alpha=0.7)
    ax.plot(dose_sorted, volume_pred_xgb,  label="XGBoost", color="tab:orange", linewidth=5, alpha=0.7)
    
    ax.set_title(f"Caso {caso_id}", fontsize=subplot_title_size)
    ax.grid(True); ax.tick_params(axis='both', which='major', labelsize=tick_label_size)

for j in range(len(caso_ids_test), len(axes)):
    fig.delaxes(axes[j])

handles, labels = axes[0].get_legend_handles_labels()
leg = fig.legend(handles, labels, loc='upper right', ncol=3, prop={'size': legend_font_size})
for txt in leg.get_texts(): txt.set_fontsize(legend_font_size)

# fig.suptitle(f"DVH ({ORGAO}) - Volumes Reais vs. Preditos", fontsize=suptitle_size)
fig.text(0.5, -0.01, 'Dose (% da prescrição)', ha='center', fontsize=axis_labels_size)
fig.text(0, 0.5, 'Volume (%)', va='center', rotation='vertical', fontsize=axis_labels_size)
plt.tight_layout(rect=[0.005, 0, 1, 0.95]); plt.show()


