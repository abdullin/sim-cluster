using System.Collections.Generic;

namespace SimMach.Playground {
    public interface IBot {
        IList<BotIssue> Verify();
        IEngine Engine(IEnv e);
    }
    
    public struct BotIssue {
        public readonly string Field;
        public readonly object Expected;
        public readonly object Actual;

        public BotIssue(string field, object expected, object actual) {
            Field = field;
            Expected = expected;
            Actual = actual;
        }
    }

}