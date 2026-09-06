using NUnit.Framework;

/// <summary>
/// OfflineRewardController 의 팝업 문구 변환(FormatRewardMessage) 검증. (기획서 6.3, 2단계-3)
/// 순수 static 함수라 씬/플레이모드가 필요 없다. UI 배선 자체는 플레이모드 스크린샷으로 확인한다.
/// </summary>
public class OfflineRewardControllerTests
{
    [Test]
    public void 결과가_null이면_보상없음_문구를_돌려준다()
    {
        Assert.AreEqual(
            "쌓인 오프라인 보상이 없습니다.",
            OfflineRewardController.FormatRewardMessage(null));
    }

    [Test]
    public void rejected_결과면_보상없음_문구를_돌려준다()
    {
        var result = new OfflineRewardResult { rejected = true };
        Assert.AreEqual(
            "쌓인 오프라인 보상이 없습니다.",
            OfflineRewardController.FormatRewardMessage(result));
    }

    [Test]
    public void 경과가_짧아_획득량이_모두_0이면_보상없음_문구를_돌려준다()
    {
        var result = new OfflineRewardResult
        {
            elapsedSeconds = 0.4,
            gainedExp = 0,
            gainedGold = 0,
            rejected = false,
        };

        Assert.IsFalse(OfflineRewardController.HasReward(result));
        Assert.AreEqual(
            "쌓인 오프라인 보상이 없습니다.",
            OfflineRewardController.FormatRewardMessage(result));
    }

    [Test]
    public void 정상_결과면_경과시간과_획득량이_문구에_담긴다()
    {
        var result = new OfflineRewardResult
        {
            elapsedSeconds = 3661, // 1시간 1분 1초
            gainedExp = 1234,
            gainedGold = 567,
            capped = false,
        };

        string msg = OfflineRewardController.FormatRewardMessage(result);

        StringAssert.Contains("1시간 1분 1초", msg);
        StringAssert.Contains("1234", msg);
        StringAssert.Contains("567", msg);
        StringAssert.DoesNotContain("최대 인정", msg);
    }

    [Test]
    public void 캡이_걸린_결과면_최대인정_안내가_덧붙는다()
    {
        var result = new OfflineRewardResult
        {
            elapsedSeconds = OfflineRewardCalculator.DefaultMaxRewardSeconds,
            gainedExp = 288000,
            gainedGold = 144000,
            capped = true,
        };

        string msg = OfflineRewardController.FormatRewardMessage(result);

        StringAssert.Contains("8시간 0분 0초", msg);
        StringAssert.Contains("최대 인정 시간에 도달", msg);
    }
}
