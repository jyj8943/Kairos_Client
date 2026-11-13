using NUnit.Framework;

/// <summary>
/// 인게임 콤보 애니메이션 계산(ComboMotion) EditMode 테스트.
/// 기본값: 오름 30px · 페이드 인 0.05초 · 오름 0.13초(ease-out quad), 0.67초 쉬면 0.07초에 걸쳐 8px 내려가며 숨김.
/// </summary>
public class ComboMotionTests
{
    private const float Eps = 1e-4f;

    private static ComboMotion Create()
    {
        return new ComboMotion();
    }

    /// <summary>오름 시작: offset = riseOffset, 숫자 알파 0, 그룹은 바로 보임</summary>
    [Test]
    public void Rise_StartsBelowAndTransparent()
    {
        var m = Create();
        Assert.IsTrue(m.SetCombo(1));
        Assert.AreEqual(30f, m.NumberOffset, Eps);
        Assert.AreEqual(0f, m.NumberAlpha, Eps);
        Assert.AreEqual(1f, m.GroupAlpha, Eps, "숨겨진 상태에서 오르면 그룹은 바로 보인다");
        Assert.AreEqual(0f, m.GroupOffset, Eps);
    }

    /// <summary>fadeIn 시점에 알파 1, rise 시점에 offset 0, 그 뒤로는 그대로</summary>
    [Test]
    public void Rise_FadeInThenSettles()
    {
        var m = Create();
        m.SetCombo(1);
        m.Tick(0.025f);
        Assert.AreEqual(0.5f, m.NumberAlpha, Eps, "페이드 인 절반");
        m.Tick(0.025f);
        Assert.AreEqual(1f, m.NumberAlpha, Eps, "0.05초에 불투명");
        Assert.Greater(m.NumberOffset, 0f, "0.05초에는 아직 올라오는 중");
        m.Tick(0.08f);
        Assert.AreEqual(0f, m.NumberOffset, Eps, "0.13초에 제자리");
        m.Tick(0.1f);
        Assert.AreEqual(0f, m.NumberOffset, Eps);
        Assert.AreEqual(1f, m.NumberAlpha, Eps);
    }

    /// <summary>ease-out quad: 절반 시간에 남은 거리가 riseOffset/2보다 작다 (= 0.25 × riseOffset)</summary>
    [Test]
    public void Rise_IsEaseOut()
    {
        var m = Create();
        m.SetCombo(1);
        m.Tick(0.065f);
        Assert.Less(m.NumberOffset, 15f);
        Assert.AreEqual(30f * 0.25f, m.NumberOffset, Eps);
    }

    /// <summary>애니메이션 도중 다시 오르면 처음부터</summary>
    [Test]
    public void Rise_RestartsOnNextIncrease()
    {
        var m = Create();
        m.SetCombo(1);
        m.Tick(0.1f);
        Assert.Less(m.NumberOffset, 30f);
        Assert.IsTrue(m.SetCombo(2));
        Assert.AreEqual(30f, m.NumberOffset, Eps);
        Assert.AreEqual(0f, m.NumberAlpha, Eps);
        Assert.AreEqual(0f, m.Elapsed, Eps);
    }

    /// <summary>쉬면 숨기기: 0.67초 전에는 보임, 0.67 + 0.07초 뒤 그룹 알파 0·8px 아래. 그 뒤 오르면 바로 보임</summary>
    [Test]
    public void Idle_HidesAfterDelayAndReappears()
    {
        var m = Create();
        m.SetCombo(5);
        m.Tick(0.66f);
        Assert.AreEqual(1f, m.GroupAlpha, Eps, "0.66초: 보임");
        Assert.AreEqual(0f, m.GroupOffset, Eps);
        m.Tick(0.01f + 0.035f);
        Assert.AreEqual(0.5f, m.GroupAlpha, 1e-3f, "사라지는 중간");
        Assert.AreEqual(4f, m.GroupOffset, 1e-2f);
        m.Tick(0.036f); // float 합이 0.74에 조금 못 미칠 수 있어 조금 더 (끝에서 멈춘다)
        Assert.AreEqual(0f, m.GroupAlpha, Eps, "0.74초: 숨김");
        Assert.AreEqual(0.74f, m.Elapsed, Eps, "다 숨긴 뒤로는 시간을 더 세지 않는다");
        Assert.AreEqual(8f, m.GroupOffset, Eps);
        Assert.IsFalse(m.IsVisible);
        m.Tick(10f);
        Assert.AreEqual(0f, m.GroupAlpha, Eps, "오래 쉬어도 숨김 그대로");

        Assert.IsTrue(m.SetCombo(6));
        Assert.AreEqual(1f, m.GroupAlpha, Eps, "다시 오르면 바로 보임");
        Assert.AreEqual(0f, m.GroupOffset, Eps, "그룹 위치도 바로 제자리");
        Assert.AreEqual(30f, m.NumberOffset, Eps, "숫자는 오름 애니메이션 처음부터");
    }

    /// <summary>hideWhenIdle = false면 오래 쉬어도 숨기지 않는다</summary>
    [Test]
    public void Idle_DisabledKeepsVisible()
    {
        var m = Create();
        m.HideWhenIdle = false;
        m.SetCombo(3);
        m.Tick(5f);
        Assert.AreEqual(1f, m.GroupAlpha, Eps);
        Assert.AreEqual(0f, m.GroupOffset, Eps);
        Assert.AreEqual(0f, m.NumberOffset, Eps);
        Assert.AreEqual(1f, m.NumberAlpha, Eps);
    }

    /// <summary>같은 값 SetCombo는 애니메이션도 쉬는 시간 초기화도 하지 않는다 (롱노트 종합 판정)</summary>
    [Test]
    public void SameValue_DoesNothing()
    {
        var m = Create();
        m.SetCombo(7);
        m.Tick(0.5f);
        Assert.IsFalse(m.SetCombo(7));
        Assert.AreEqual(0.5f, m.Elapsed, Eps, "쉬는 시간이 그대로 이어진다");
        m.Tick(0.25f);
        Assert.AreEqual(0f, m.GroupAlpha, Eps, "같은 값이 와도 0.67 + 0.07초면 숨김");
    }

    /// <summary>0이면 애니메이션 없이 바로 숨김. 그 뒤 1이 되면 다시 오름</summary>
    [Test]
    public void Zero_HidesImmediately()
    {
        var m = Create();
        m.SetCombo(10);
        m.Tick(0.2f);
        Assert.IsFalse(m.SetCombo(0));
        Assert.AreEqual(0, m.Combo);
        Assert.AreEqual(0f, m.GroupAlpha, Eps);
        Assert.AreEqual(0f, m.NumberAlpha, Eps);
        Assert.AreEqual(0f, m.GroupOffset, Eps);
        Assert.IsFalse(m.IsVisible);
        m.Tick(1f);
        Assert.AreEqual(0f, m.GroupAlpha, Eps);

        Assert.IsTrue(m.SetCombo(1));
        Assert.AreEqual(1f, m.GroupAlpha, Eps);
        Assert.AreEqual(30f, m.NumberOffset, Eps);
    }

    /// <summary>Tick을 안 부르는 동안(일시정지)에는 상태가 바뀌지 않는다. 0·음수 Tick도 그대로</summary>
    [Test]
    public void NoTick_StateFrozen()
    {
        var m = Create();
        m.SetCombo(4);
        m.Tick(0.03f);
        float offset = m.NumberOffset;
        float alpha = m.NumberAlpha;
        float group = m.GroupAlpha;
        m.Tick(0f);
        m.Tick(-1f);
        Assert.AreEqual(offset, m.NumberOffset, Eps);
        Assert.AreEqual(alpha, m.NumberAlpha, Eps);
        Assert.AreEqual(group, m.GroupAlpha, Eps);
        Assert.AreEqual(0.03f, m.Elapsed, Eps);

        // 쉬는 시간도 멈춘다: 0.6초에서 멈춘 뒤 재개해 0.1초 더 지나야 숨기기 시작
        m.Tick(0.57f);
        Assert.AreEqual(1f, m.GroupAlpha, Eps);
        m.Tick(0.1f);
        Assert.Less(m.GroupAlpha, 1f);
    }

    /// <summary>Reset: 처음 상태 (콤보 0, 숨김)</summary>
    [Test]
    public void Reset_Hidden()
    {
        var m = Create();
        m.SetCombo(99);
        m.Tick(0.1f);
        m.Reset();
        Assert.AreEqual(0, m.Combo);
        Assert.AreEqual(0f, m.Elapsed, Eps);
        Assert.AreEqual(0f, m.GroupAlpha, Eps);
        Assert.IsTrue(m.SetCombo(1), "Reset 뒤 1은 오름");
    }
}
