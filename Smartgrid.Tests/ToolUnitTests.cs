using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartGrid;

namespace Smartgrid.Tests
{
    public class ToolUnitTests
    {
        [Theory]
        [InlineData("cable", ToolType.Cable)]
        [InlineData("CABLE", ToolType.Cable)]
        [InlineData("  cable  ", ToolType.Cable)]
        [InlineData("sensor", ToolType.Sensor)]
        [InlineData("switch", ToolType.Switch)]
        [InlineData("battery", ToolType.Battery)]
        [InlineData("meter", ToolType.SmartMeter)]
        [InlineData("smartmeter", ToolType.SmartMeter)]
        [InlineData("smart_meter", ToolType.SmartMeter)]
        [InlineData("demand", ToolType.SmartMeter)]
        [InlineData("demandresponse", ToolType.SmartMeter)]
        [InlineData("demand_response", ToolType.SmartMeter)]
        [InlineData("predictor", ToolType.Predictor)]
        [InlineData("v2g", ToolType.V2G)]
        [InlineData("V2G", ToolType.V2G)]
        public void TryParse_RecognisesKnownKeys(string key, ToolType expected)
        {
            bool ok = Tool.TryParse(key, out ToolType tool);
            Assert.True(ok);
            Assert.Equal(expected, tool);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-tool")]
        [InlineData("cabel")] // typo
        public void TryParse_UnknownKey_ReturnsFalseAndDefaultsToCable(string key)
        {
            bool ok = Tool.TryParse(key, out ToolType tool);
            Assert.False(ok);
            Assert.Equal(ToolType.Cable, tool);
        }

        [Fact]
        public void Conducts_OnlySensorDoesNotConduct()
        {
            foreach (ToolType tool in Enum.GetValues<ToolType>())
            {
                bool expected = tool != ToolType.Sensor;
                Assert.Equal(expected, Tool.Conducts(tool));
            }
        }

        [Fact]
        public void Name_And_Hint_AreNonEmpty_ForEveryTool()
        {
            foreach (ToolType tool in Enum.GetValues<ToolType>())
            {
                Assert.False(string.IsNullOrWhiteSpace(Tool.Name(tool)));
                Assert.False(string.IsNullOrWhiteSpace(Tool.Hint(tool)));
            }
        }

        [Fact]
        public void ShortLabel_CableIsBlank_ByDesign()
        {
            // Cable is drawn as a plain wire with no text label - documented
            // behaviour in Tool.cs, worth pinning down explicitly.
            Assert.Equal("", Tool.ShortLabel(ToolType.Cable));
        }
    }
}
