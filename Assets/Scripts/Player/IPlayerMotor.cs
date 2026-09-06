/// <summary>
/// 플레이어 이동을 "실제로 수행하는 쪽"의 계약.
/// 수동 입력 핸들러(PlayerInputHandler)와 자동전투 FSM 은 이 인터페이스만 알고 호출한다.
/// 즉 "누가 이동을 명령하는가"와 "이동을 어떻게 수행하는가"를 분리한다. (기획서 2.4)
/// </summary>
public interface IPlayerMotor
{
    /// <summary>수평 이동 명령. 부호만 사용하며 0 이면 즉시 정지. 매 프레임 호출되는 지속 입력.</summary>
    void MoveHorizontal(float direction);

    /// <summary>점프 요청. 접지 상태일 때만 실제로 점프한다.</summary>
    void Jump();

    /// <summary>아래로 점프(드롭다운) 요청. 원웨이 플랫폼 위에 서 있을 때만 동작한다. (기획서 2.3)</summary>
    void DropDown();
}
