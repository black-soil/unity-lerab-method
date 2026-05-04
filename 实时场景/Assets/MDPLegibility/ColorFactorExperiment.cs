using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class ColorFactorExperiment : MonoBehaviour
{
    [System.Serializable]
    public class ExperimentConfig
    {
        public string name;
        public float variance = 7225f;
        public float weight = 2.0f;
        public float decay = 0.0f;
    }
    
    [Header("实验配置")]
    [SerializeField] private List<ExperimentConfig> experiments = new List<ExperimentConfig>();
    [SerializeField] public  int startState = 16;
    [SerializeField] public int assumedTrueGoal = 1; // 目标索引
    
    [Header("自动运行")]
    [SerializeField] private bool autoRunAll = true;
    [SerializeField] private float delayBetweenRuns = 1.0f;
    
    private BayesianGoalObserver bayesObserver;
    private MDPLegibilityPlanner planner;
    private LegibleAgentController agent;
    private int currentExperiment = 0;
    
    void Start()
    {
        bayesObserver = FindObjectOfType<BayesianGoalObserver>();
        planner = FindObjectOfType<MDPLegibilityPlanner>();
        agent = FindObjectOfType<LegibleAgentController>();
        
        if (autoRunAll && experiments.Count > 0)
        {
            StartCoroutine(RunAllExperiments());
        }
    }
    
    IEnumerator RunAllExperiments()
    {
        Debug.Log($"🎯 开始颜色因子参数实验，共{experiments.Count}组");
        
        for (int i = 0; i < experiments.Count; i++)
        {
            yield return StartCoroutine(RunSingleExperiment(i));
            yield return new WaitForSeconds(delayBetweenRuns);
        }
        
        Debug.Log($"✅ 所有实验完成！结果已保存到 {Application.dataPath}/BeliefExports/");
    }
    
    IEnumerator RunSingleExperiment(int expIndex)
    {
        var config = experiments[expIndex];
        Debug.Log($"\n=== 实验 {expIndex+1}/{experiments.Count}: {config.name} ===");
        Debug.Log($"参数: 方差={config.variance}, 权重={config.weight}, 衰减={config.decay}");
        
        // 1. 配置参数
        ConfigureParameters(config);
        
        // 2. 重置环境
        agent.ResetAgent();
        yield return new WaitForEndOfFrame();
        
        // 3. 运行智能体到目标
        yield return StartCoroutine(RunAgentToGoal());
        
        // 4. 导出结果
        ExportExperimentResult(config, expIndex);
    }
    
    private void ConfigureParameters(ExperimentConfig config)
    {
        // 设置目标
        planner.assumedTrueGoal = assumedTrueGoal;
        planner.UpdateLegiblePolicy();
        

        
        // 通过反射设置私有字段，或将这些字段改为public
        // 这里假设您已将字段设为public或添加了setter
        // bayesObserver.SetColorParameters(config.variance, config.weight, config.decay);
        // 仅设置颜色参数（如果支持）
        if (bayesObserver != null)
    {
        // 直接设置公共属性
        bayesObserver.ColorVariance = config.variance;
        bayesObserver.ColorWeightMultiplier = config.weight;
        bayesObserver.ColorDecay = config.decay;
        
        Debug.Log($"颜色参数已设置: 方差={config.variance}, 权重={config.weight}, 衰减={config.decay}");
    }
    
        Debug.Log($"已配置: 目标={assumedTrueGoal}");
        
    }
    
    IEnumerator RunAgentToGoal()
    {
        // 启用自动巡航
        agent.StartAutoCruise();
        
        // 等待智能体到达目标
        int maxSteps = 20;
        int steps = 0;
        
        while (!agent.grid.IsGoal(agent.GetCurrentState()) && steps < maxSteps)
        {
            yield return new WaitForSeconds(0.5f);
            steps++;
        }
        
        agent.StopAutoCruise();
        
        if (steps >= maxSteps)
            Debug.LogWarning("⚠️ 未在步数限制内到达目标");
        else
            Debug.Log($"✅ 到达目标，共{steps}步");
    }
    
    private void ExportExperimentResult(ExperimentConfig config, int expIndex)
    {
        // 导出信念历史
        bayesObserver.ExportBeliefHistoryToJson();
        
        // 创建实验摘要
        string summary = $"实验: {config.name}\n" +
                        $"参数: 方差={config.variance}, 权重={config.weight}, 衰减={config.decay}\n" +
                        $"目标: 目标{assumedTrueGoal} (状态{agent.grid.GOAL_STATES[assumedTrueGoal]})\n" +
                        $"起始状态: {startState}\n" +
                        $"时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        
        Debug.Log($"📄 实验摘要:\n{summary}");
        
        // 保存摘要到文件
        string directory = Application.dataPath + "/BeliefExports/";
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
            
        string filename = $"color_exp_{expIndex+1:00}_{config.name.Replace(" ", "_")}.txt";
        File.WriteAllText(Path.Combine(directory, filename), summary);
    }
    
    [ContextMenu("手动运行下一组实验")]
    public void RunNextExperiment()
    {
        if (currentExperiment < experiments.Count)
        {
            StartCoroutine(RunSingleExperiment(currentExperiment));
            currentExperiment++;
        }
    }
}