import json
import glob
import numpy as np
import matplotlib.pyplot as plt
import matplotlib.cm as cm
from scipy.interpolate import make_interp_spline
from matplotlib.font_manager import FontProperties
import matplotlib
import platform


# 设置字体（根据您的系统选择）
def set_chinese_font():
    """设置中文字体"""
    # Windows系统字体
    if platform.system() == 'Windows':
        font_path = "C:/Windows/Fonts/msyh.ttc"  # 微软雅黑
    elif platform.system() == 'Darwin':  # macOS
        font_path = "/System/Library/Fonts/PingFang.ttc"  # 苹方
    else:  # Linux
        font_path = "/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf"

    try:
        # 尝试加载中文字体
        font_prop = FontProperties(fname=font_path)
        matplotlib.rcParams['font.sans-serif'] = [font_prop.get_name()]
        matplotlib.rcParams['axes.unicode_minus'] = False
        print(f"成功设置字体: {font_path}")
    except:
        # 如果失败，使用默认字体
        matplotlib.rcParams['font.sans-serif'] = ['DejaVu Sans']
        matplotlib.rcParams['axes.unicode_minus'] = False
        print("使用默认字体")


# 在程序开始处调用
set_chinese_font()

def load_and_analyze(json_file):
    """加载单个JSON文件并计算关键指标"""
    with open(json_file, 'r', encoding='utf-8') as f:
        data = json.load(f)

    # 提取目标1（假设goal_1是索引1）的信念序列
    belief_series = []
    for record in data.get('history', []):
        if 'belief' in record and len(record['belief']) > 1:
            belief_series.append(record['belief'][2])  # goal_1的信念

    if not belief_series:
        return None

    # 1. 最终信念
    final_belief = belief_series[-1]

    # 2. 收敛步数（信念首次超过阈值，如0.9）
    convergence_step = None
    for i, belief in enumerate(belief_series):
        if belief > 0.85:
            convergence_step = i
            break
    # 如果从未超过0.9，用总步数代替（表示收敛慢）
    if convergence_step is None:
        convergence_step = len(belief_series)

    # 3. 信念稳定性（用序列标准差衡量，越小越稳定）
    stability = 1 / (np.std(belief_series) + 1e-6)  # 取倒数，值越大越稳定

    return {
        'final_belief': final_belief,
        'convergence_step': convergence_step,
        'stability': stability,
        'belief_series': belief_series
    }


def calculate_composite_score(analysis_result, max_steps):
    """计算综合性能评分 (0-1)"""
    fb = analysis_result['final_belief']  # 最终信念，理论最大1
    cs = analysis_result['convergence_step']  # 收敛步数，越小越好
    st = analysis_result['stability']  # 稳定性，越大越好

    # 归一化并加权（权重可调）
    # 最终信念权重高（0.6），收敛速度（0.3），稳定性（0.1）
    score = (0.7 * fb) + (0.1 * (1 - cs / max_steps)) + (0.2 * (st / 10))  # 稳定性除以一个基准值
    return max(0, min(1, score))  # 确保在0-1之间


# --- 主程序开始 ---
# 1. 查找并加载所有JSON文件
json_files = glob.glob("belief_history_goal*.json")
if not json_files:
    print("未找到 belief_history_goal*.json 文件，请确保脚本放在数据目录下。")
    exit()

sigma_values = []  # 存储sigma值（需要从文件名或摘要中提取，这里假设已按顺序）
all_results = []
all_scores = []

# 假设您的JSON文件是按sigma顺序生成的（如 sigma_70.json, sigma_85.json, sigma_100.json）
# 这里我们按文件名排序，并手动指定sigma值列表
# 请根据您的实际情况修改这个列表
assumed_sigmas = [ 0.2, 0.4, 0.6, 0.8,1, 1.2, 1.4, 1.6, 1.8, 2]  # 必须与json_files顺序对应

for sigma, file in zip(assumed_sigmas, sorted(json_files)):
    result = load_and_analyze(file)
    if result:
        sigma_values.append(sigma)
        all_results.append(result)
        print(f"σ={sigma}: 最终信念={result['final_belief']:.3f}, 收敛步数={result['convergence_step']}")

# 2. 计算综合评分
max_possible_steps = max(r['convergence_step'] for r in all_results)
for result in all_results:
    score = calculate_composite_score(result, max_possible_steps)
    all_scores.append(score)

# 3. 绘制超参数敏感性分析图
fig, ax = plt.subplots(figsize=(10, 6))

# 为稳定性创建颜色映射
stabilities = [r['stability'] for r in all_results]
norm = plt.Normalize(min(stabilities), max(stabilities))
cmap = cm.viridis

# 绘制曲线（略微平滑以便观察趋势）
x_smooth = np.linspace(min(sigma_values), max(sigma_values), 300)
if len(sigma_values) >= 3:
    spl = make_interp_spline(sigma_values, all_scores, k=2)  # k=2 二次样条
    y_smooth = spl(x_smooth)
    ax.plot(x_smooth, y_smooth, 'b-', alpha=0.4, linewidth=2, label='性能趋势线')
else:
    ax.plot(sigma_values, all_scores, 'b--', alpha=0.4, linewidth=2, label='性能趋势线')

# 绘制散点（点大小=收敛速度，点颜色=稳定性）
scatter = ax.scatter(
    sigma_values,
    all_scores,
    s=150 - np.array([r['convergence_step'] for r in all_results]) * 3,  # 点大小：收敛步数越小，点越大
    c=stabilities,
    cmap=cmap,
    edgecolors='black',
    linewidth=1.5,
    zorder=5
)

# 标注每个点
for sigma, score, result in zip(sigma_values, all_scores, all_results):
    ax.annotate(f'σ={sigma}\n{score:.2f}',
                xy=(sigma, score),
                xytext=(0, 10),
                textcoords='offset points',
                ha='center',
                fontsize=9,
                bbox=dict(boxstyle="round,pad=0.3", facecolor="wheat", alpha=0.7))

# # 标注最优值
optimal_idx = np.argmax(all_scores)
# ax.plot(sigma_values[optimal_idx], all_scores[optimal_idx], 'r*', markersize=20,
#         label=f'最优 σ={sigma_values[optimal_idx]}')

# 图表装饰
ax.set_xlabel('速度匹配度参数 (σ)', fontsize=12, fontweight='bold')
ax.set_ylabel('综合信念识别率', fontsize=12, fontweight='bold')
ax.set_title('速度因子σ超参数敏感性分析', fontsize=14, fontweight='bold', pad=20)
ax.grid(True, alpha=0.3)
ax.set_ylim([0, 1.1])
ax.legend(loc='best')

# 添加颜色条表示稳定性
cbar = plt.colorbar(scatter, ax=ax)
cbar.set_label('信念稳定性 ', rotation=270, labelpad=15)

# 添加图例说明点大小
import matplotlib.lines as mlines

step_legend_elements = [
    mlines.Line2D([0], [0], marker='o', color='w', label='点大 → 收敛快',
                  markerfacecolor='grey', markersize=10, markeredgecolor='black'),
    mlines.Line2D([0], [0], marker='o', color='w', label='点小 → 收敛慢',
                  markerfacecolor='grey', markersize=5, markeredgecolor='black')
]
ax.legend(handles=step_legend_elements, loc='upper left', fontsize=9, framealpha=0.7)
ax.legend(loc='lower right')  # 重新添加主图例

plt.tight_layout()
plt.savefig('sigma_hyperparameter_sensitivity.png', dpi=300, bbox_inches='tight')
plt.show()

print(f"\n分析完成！图表已保存为 'sigma_hyperparameter_sensitivity.png'")
print(f"最优σ值为: {sigma_values[optimal_idx]} (综合评分: {all_scores[optimal_idx]:.3f})")