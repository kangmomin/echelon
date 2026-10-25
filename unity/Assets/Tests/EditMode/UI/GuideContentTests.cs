using System.Text;
using System.Text.RegularExpressions;
using Echelon.Unity.UI.Guide;
using NUnit.Framework;

namespace Echelon.Unity.Tests.UI
{
    public sealed class GuideContentTests
    {
        [Test]
        public void Sections_ContainAllElevenEntriesInGodotOrder()
        {
            var expected = new[]
            {
                "시작하기",
                "조작",
                "제대와 지휘 지연",
                "병과와 장비",
                "항공과 공병",
                "대형과 이동 방식",
                "탐지와 교전",
                "화력 지원과 보급",
                "편성과 배치",
                "승리 조건",
                "요령"
            };

            Assert.That(GuideContent.Sections.Count, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
                Assert.That(GuideContent.Sections[i].Title, Is.EqualTo(expected[i]));
        }

        [Test]
        public void EverySection_HasKoreanTitleAndBody()
        {
            foreach (var section in GuideContent.Sections)
            {
                Assert.That(Regex.IsMatch(section.Title, "[가-힣]"), section.Title);
                Assert.That(Regex.IsMatch(BodyText(section), "[가-힣]"), section.Title);
            }
        }

        [Test]
        public void SectionMarkers_ArePresent()
        {
            var markers = new[]
            {
                "제대(梯隊)",
                "Ctrl+1~9 / 1~9",
                "대대 → 중대 약 5초",
                "T-80BVM",
                "반경 90 m 진지",
                "도로 ×1.4",
                "사기 20 이하",
                "분당 20 단위",
                "T+20분(20% 할인)",
                "25% 이하",
                "약 10초 뒤에 움직인다"
            };

            for (var i = 0; i < markers.Length; i++)
                Assert.That(BodyText(GuideContent.Sections[i]), Does.Contain(markers[i]));
        }

        [Test]
        public void VictorySection_ContainsCommittedForceTwentyFivePercentRule()
        {
            var text = BodyText(GuideContent.Sections[9]);
            Assert.That(text, Does.Contain("현재 전장 전투력(장비 가치 기준)이 지금까지 투입된 부대 가치의 25% 이하"));
            Assert.That(text, Does.Contain("그쪽이 즉시 패배"));
            Assert.That(text, Does.Contain("대기 증원은 계산에서 제외"));
        }

        [Test]
        public void ControlsAndFormations_UseStructuredTables()
        {
            var tableCount = 0;
            foreach (var section in GuideContent.Sections)
            {
                foreach (var block in section.Blocks)
                {
                    if (block.Kind == GuideBlockKind.Table)
                        tableCount++;
                }
            }

            Assert.That(tableCount, Is.EqualTo(2));
            Assert.That(OnlyTable(GuideContent.Sections[1]).Rows.Count, Is.EqualTo(28));
            Assert.That(OnlyTable(GuideContent.Sections[5]).Rows.Count, Is.EqualTo(7));
        }

        [Test]
        public void RichText_UsesBalancedTmpTagsAndNoGodotBbcode()
        {
            var unsupported = new[]
            {
                "[b]",
                "[/b]",
                "[color=",
                "[/color]",
                "[font_size",
                "[/font_size]",
                "[table",
                "[/table]",
                "[cell]",
                "[/cell]"
            };

            foreach (var section in GuideContent.Sections)
            {
                foreach (var block in section.Blocks)
                {
                    AssertTags(block.Text, unsupported);
                    foreach (var row in block.Rows)
                    {
                        AssertTags(row.Key, unsupported);
                        AssertTags(row.Description, unsupported);
                    }
                }
            }
        }

        private static GuideBlock OnlyTable(GuideSection section)
        {
            GuideBlock result = null;
            foreach (var block in section.Blocks)
            {
                if (block.Kind != GuideBlockKind.Table)
                    continue;
                Assert.That(result, Is.Null, section.Title + " should have one table.");
                result = block;
            }
            Assert.That(result, Is.Not.Null, section.Title + " should have a table.");
            return result;
        }

        private static string BodyText(GuideSection section)
        {
            var text = new StringBuilder();
            foreach (var block in section.Blocks)
            {
                text.AppendLine(block.Text);
                foreach (var row in block.Rows)
                {
                    text.AppendLine(row.Key);
                    text.AppendLine(row.Description);
                }
            }
            return text.ToString();
        }

        private static void AssertTags(string text, string[] unsupported)
        {
            foreach (var token in unsupported)
                Assert.That(text, Does.Not.Contain(token));
            Assert.That(text, Does.Not.Contain("<b></b>"));
            Assert.That(Count(text, "<b>"), Is.EqualTo(Count(text, "</b>")), text);
            Assert.That(Count(text, "<size="), Is.EqualTo(Count(text, "</size>")), text);
            Assert.That(Count(text, "<color="), Is.EqualTo(Count(text, "</color>")), text);
        }

        private static int Count(string text, string token)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(token, index)) >= 0)
            {
                count++;
                index += token.Length;
            }
            return count;
        }
    }
}
