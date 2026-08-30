/// <summary>
/// 데미지 계산 1회의 결과. 순수 데이터.
/// </summary>
public struct DamageResult
{
    /// <summary>최종 데미지(내림 처리된 정수, 최소 1).</summary>
    public int Amount;

    /// <summary>이번 공격이 크리티컬로 발동했는지.</summary>
    public bool IsCritical;
}
