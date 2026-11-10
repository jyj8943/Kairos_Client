/// <summary>
/// 키를 누르고 있을 때의 반복 입력 (배속 키 등). 누른 순간 한 번, RepeatDelaySeconds 뒤부터 RepeatIntervalSeconds마다 한 번씩.
/// 시간은 바깥에서 넘긴다 (realtime). Unity API를 쓰지 않아 EditMode 테스트로 검사할 수 있다.
/// </summary>
public class KeyRepeat
{
    private readonly double delaySeconds;
    private readonly double intervalSeconds;
    private bool isHeld;
    private double nextRepeatTime;

    public KeyRepeat(double delaySeconds, double intervalSeconds)
    {
        this.delaySeconds = delaySeconds;
        this.intervalSeconds = intervalSeconds;
    }

    /// <summary>누르고 있으면 true</summary>
    public bool IsHeld => isHeld;

    /// <summary>누른 순간. 이때 한 번 동작해야 한다 (반환값 1).</summary>
    public int Press(double now)
    {
        isHeld = true;
        nextRepeatTime = now + delaySeconds;
        return 1;
    }

    /// <summary>뗀 순간 (또는 입력을 끊을 때).</summary>
    public void Release()
    {
        isHeld = false;
    }

    /// <summary>
    /// 매 프레임 부른다. 지난 프레임 뒤로 반복 시각이 몇 번 지났는지 돌려준다 (프레임이 늦으면 2 이상일 수 있다).
    /// </summary>
    public int Tick(double now)
    {
        if (!isHeld) return 0;

        int count = 0;
        while (now >= nextRepeatTime)
        {
            count++;
            nextRepeatTime += intervalSeconds;
        }
        return count;
    }
}
