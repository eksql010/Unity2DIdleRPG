/// <summary>
/// 오프라인 보상 계산 결과. 순수 데이터 컨테이너(직렬화/유니티 의존 없음).
/// </summary>
public class OfflineRewardResult
{
    /// <summary>실제 경과 시간(초). 캡 적용 전 값.</summary>
    public double elapsedSeconds;

    /// <summary>보상 계산에 사용된 경과 시간(초). 최대 캡이 적용된 값.</summary>
    public double cappedSeconds;

    /// <summary>획득 경험치(내림 처리된 정수).</summary>
    public int gainedExp;

    /// <summary>획득 골드(내림 처리된 정수).</summary>
    public int gainedGold;

    /// <summary>경과 시간이 최대 캡을 초과해서 캡으로 고정되었는지 여부.</summary>
    public bool wasCapped;

    /// <summary>지급할 보상이 실제로 존재하는지(경험치 또는 골드 &gt; 0).</summary>
    public bool HasReward => gainedExp > 0 || gainedGold > 0;
}
