using UnityEngine;

public class TestBayesianStep : MonoBehaviour
{
    // 去掉 SerializeField，运行时自动找
    private BayesianGoalObserver bayes;
    [SerializeField] int testState = 17;
    [SerializeField] int testAction = 0;
    [SerializeField] int testNextState = 9;

    void Start()
    {
        bayes = FindObjectOfType<BayesianGoalObserver>();
        if (!bayes) Debug.LogError("没找到 BayesianGoalObserver！");
    }

    [ContextMenu("执行单步贝叶斯更新")]
    void ManualStep()
    {
        if (!bayes) return;
        
        Vector3 fakeForward = Vector3.forward;
        float fakeSpeed = 1.25f;
        float fakeColor = 128f;
        
        var beliefs = bayes.UpdateBelief(
            testState, testAction, testNextState, 
            fakeForward, fakeSpeed, fakeColor
        );
        
        Debug.Log($"更新后信念: G0={beliefs[0]:F3}, G1={beliefs[1]:F3}, G2={beliefs[2]:F3}, G3={beliefs[3]:F3}");
    }

    [ContextMenu("重置信念")]
    void ResetTest() => bayes?.ResetBeliefs();
}