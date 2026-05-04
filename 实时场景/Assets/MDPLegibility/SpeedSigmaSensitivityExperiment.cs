using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class SpeedSigmaSensitivityExperiment : MonoBehaviour
{
    [System.Serializable]
    public class SigmaTestConfig
    {
        public string testName;          // 测试名称，如 "Sigma_70"
        public float sigmaValue;         // 标准差 σ，如 70
        public float variance { get { return sigmaValue * sigmaValue; } } // 自动计算方差 σ²
    }
    
    [Header("速度σ灵敏度测试配置")]  // 修改标题
    [SerializeField] private List<SigmaTestConfig> sigmaTests = new List<SigmaTestConfig>();
    [SerializeField] private int startState = 16;
    [SerializeField] public int assumedTrueGoal = 2; // 目标索引
    
    [Header("自动运行")]
    [SerializeField] private bool autoRunAll = true;
    [SerializeField] private float delayBetweenRuns = 1.0f;
    
    private BayesianGoalObserver bayesObserver;
    private MDPLegibilityPlanner planner;
    private LegibleAgentController agent;
    private int currentTestIndex = 0;
    
    void Start()
    {
            bayesObserver = FindObjectOfType<BayesianGoalObserver>();
        planner = FindObjectOfType<MDPLegibilityPlanner>();
        agent = FindObjectOfType<LegibleAgentController>();
        
        // 修改这里：设置速度因子的测试值
        if (sigmaTests.Count == 0)
        {
            sigmaTests.Add(new SigmaTestConfig { testName = "SpeedSigma_0.5", sigmaValue = 0.5f });
            sigmaTests.Add(new SigmaTestConfig { testName = "SpeedSigma_1.0", sigmaValue = 1.0f });
            sigmaTests.Add(new SigmaTestConfig { testName = "SpeedSigma_2.0", sigmaValue = 2.0f });
        }
        
        if (autoRunAll && sigmaTests.Count > 0)
        {
            StartCoroutine(RunAllSigmaTests());
        }
    }
    
    IEnumerator RunAllSigmaTests()
    {
        Debug.Log($"🎯 开始颜色标准差(σ)灵敏度测试，共{sigmaTests.Count}组");
        Debug.Log($"测试值(σ): {string.Join(", ", sigmaTests.ConvertAll(t => t.sigmaValue))}");
        
        for (int i = 0; i < sigmaTests.Count; i++)
        {
            yield return StartCoroutine(RunSingleSigmaTest(i));
            yield return new WaitForSeconds(delayBetweenRuns);
        }
        
        Debug.Log($"✅ 所有灵敏度测试完成！结果已保存至 {Application.dataPath}/BeliefExports/");
        // 可选：自动生成汇总报告
        PrintSigmaTestSummary();
    }
    
    IEnumerator RunSingleSigmaTest(int testIndex)
    {
        var config = sigmaTests[testIndex];
        Debug.Log($"\n=== 测试 {testIndex+1}/{sigmaTests.Count}: {config.testName} (σ={config.sigmaValue}) ===");
        
        // 1. 配置参数
        ConfigureSigmaTest(config);
        
        // 2. 重置环境
        agent.ResetAgent();
        yield return new WaitForEndOfFrame();
        
        // 3. 运行智能体到目标
        yield return StartCoroutine(RunAgentToGoal());
        
        // 4. 导出结果
        ExportSigmaTestResult(config, testIndex);
    }
    
    private void ConfigureSigmaTest(SigmaTestConfig config)
    {
        if (planner == null)
        {
            Debug.LogError("Planner 引用为空！");
            return;
        }
        
        planner.assumedTrueGoal = assumedTrueGoal;
        
        try
        {
            planner.UpdateLegiblePolicy();
        }
        catch (System.NullReferenceException ex)
        {
            Debug.LogError($"更新可读性策略失败: {ex.Message}");
            Debug.LogError($"请检查 MDPLegibilityPlanner 的初始化状态");
            return;
        }
        
        // 设置目标
        planner.assumedTrueGoal = assumedTrueGoal;
        planner.UpdateLegiblePolicy();
        
        // 修改这里：设置速度因子参数而不是颜色因子参数
        if (bayesObserver != null)
        {
            bayesObserver.SpeedVariance = config.variance; // 传入速度方差值
            Debug.Log($"已设置速度参数: σ={config.sigmaValue}, σ²={config.variance:F2}");
        }
        
        // 修改这里：确保只启用速度因子
        Debug.Log($"实验配置: 目标={assumedTrueGoal}, 使用速度方差 σ²={config.variance:F2}");
    }
    
    IEnumerator RunAgentToGoal()
    {
        agent.StartAutoCruise();
        int maxSteps = 200; // 增加步数限制
        int steps = 0;
        
        // 获取指定目标的实际状态
        int targetGoalState = planner.grid.GOAL_STATES[assumedTrueGoal];
        Debug.Log($"🎯 本次测试的目标状态：{targetGoalState}");
        
        int currentState = agent.GetCurrentState();
        while (currentState != targetGoalState && steps < maxSteps)
        {
            yield return new WaitForSeconds(0.5f);
            currentState = agent.GetCurrentState();
            
            // 添加调试信息
            Debug.Log($"步骤{steps}: 当前状态={currentState}, 目标状态={targetGoalState}, 是否其他目标={agent.grid.IsGoal(currentState)}");
            
            steps++;
            
            // 如果意外到达其他目标，发出警告
            if (agent.grid.IsGoal(currentState) && currentState != targetGoalState)
            {
                Debug.LogWarning($"⚠️ 注意：已到达其他目标状态{currentState}，但这不是本次测试的目标{targetGoalState}");
            }
        }
        
        agent.StopAutoCruise();
        
        if (steps >= maxSteps) 
        {
            Debug.LogError($"❌ 在{maxSteps}步内未到达目标{targetGoalState}");
        }
        else if (currentState == targetGoalState)
        {
            Debug.Log($"✅ 成功到达指定目标{targetGoalState}，步数：{steps}");
        }
        else
        {
            Debug.LogWarning($"⚠️ 提前终止：当前状态{currentState}，目标状态{targetGoalState}");
        }
    }
    
    private void ExportSigmaTestResult(SigmaTestConfig config, int testIndex)
    {
        // 导出信念历史
        bayesObserver.ExportBeliefHistoryToJson();
        
        // 修改这里：保存速度测试摘要
        string summary = $"速度σ灵敏度测试: {config.testName}\n" +
                        $"参数: σ={config.sigmaValue}, σ²={config.variance:F2}\n" +
                        $"目标: 目标{assumedTrueGoal}\n" +
                        $"起始状态: {startState}\n" +
                        $"时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        
        string directory = Application.dataPath + "/BeliefExports/";
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            
        string filename = $"speed_sigma_test_{testIndex+1:00}_{config.testName}.txt";
        File.WriteAllText(Path.Combine(directory, filename), summary);
        
        Debug.Log($"📄 速度测试摘要已保存: {filename}");
    }
    
    [ContextMenu("手动运行下一组σ测试")]
    public void RunNextSigmaTest()
    {
        if (currentTestIndex < sigmaTests.Count)
        {
            StartCoroutine(RunSingleSigmaTest(currentTestIndex));
            currentTestIndex++;
        }
    }
    
    [ContextMenu("打印σ测试摘要")]
    private void PrintSigmaTestSummary()
    {
        // 此方法可在所有测试完成后，手动点击执行，用于快速查看控制台汇总
        Debug.Log($"=== 颜色σ灵敏度测试汇总 ({sigmaTests.Count} 组) ===");
        for (int i = 0; i < sigmaTests.Count; i++)
        {
            var config = sigmaTests[i];
            Debug.Log($"测试{i+1}: {config.testName}, σ={config.sigmaValue}, σ²={config.variance}");
        }
    }
}