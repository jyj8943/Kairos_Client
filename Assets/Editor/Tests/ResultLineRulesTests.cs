using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 결과 화면 대화창 멘트 규칙 (ResultLineRules) · 곡 감정 키워드 데이터 (SongData.EmotionKeyword, 11/25) EditMode 확인.
/// </summary>
public class ResultLineRulesTests
{
    [Test]
    public void Pick_ByRank()
    {
        (PlayRank rank, string line)[] table =
        {
            (PlayRank.AP, "완벽한 연주였어! 전부 다 기록했어."),
            (PlayRank.S, "가슴이 뛰는 노래였어. 이 느낌, 꼭 기록해 둘게!"),
            (PlayRank.A, "가슴이 뛰는 노래였어. 이 느낌, 꼭 기록해 둘게!"),
            (PlayRank.B, "좋은 노래야. 조금씩 기억이 돌아오고 있어."),
            (PlayRank.C, "좋은 노래야. 조금씩 기억이 돌아오고 있어."),
            (PlayRank.D, "좋은 노래야. 조금씩 기억이 돌아오고 있어."),
            (PlayRank.F, "괜찮아, 다시 해 보자. 노래는 어디 안 가니까.")
        };
        Assert.AreEqual(System.Enum.GetValues(typeof(PlayRank)).Length, table.Length, "모든 랭크 확인");
        StringBuilder log = new StringBuilder("[ResultLineRulesTest] 랭크별 멘트\n");
        foreach ((PlayRank rank, string line) row in table)
        {
            Assert.AreEqual(row.line, ResultLineRules.Pick(row.rank), row.rank.ToString());
            log.Append($"| {row.rank} | {ResultLineRules.Pick(row.rank)} |\n");
        }
        Debug.Log(log.ToString());
    }

    [Test]
    public void EmotionKeyword_SongAssets_AndEmpty()
    {
        SongDatabase database = AssetDatabase.LoadAssetAtPath<SongDatabase>("Assets/Data/Songs/SongDatabase.asset");
        Assert.IsNotNull(database);
        (string id, string keyword)[] expected = { ("send_my_love_tonight", "설렘"), ("critical_velocity", "해방감"), ("d_minor_descent", "격정") };
        foreach ((string id, string keyword) row in expected)
        {
            SongData song = null;
            foreach (string guid in AssetDatabase.FindAssets("t:SongData"))
            {
                SongData s = AssetDatabase.LoadAssetAtPath<SongData>(AssetDatabase.GUIDToAssetPath(guid));
                if (s.SongId == row.id) song = s;
            }
            Assert.IsNotNull(song, row.id);
            Assert.AreEqual(row.keyword, song.EmotionKeyword, row.id);
        }

        SongData empty = ScriptableObject.CreateInstance<SongData>();
        Assert.AreEqual(string.Empty, empty.EmotionKeyword, "값이 없으면 빈 문자열");
        Object.DestroyImmediate(empty);
    }
}
