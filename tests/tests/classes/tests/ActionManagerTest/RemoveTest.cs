using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CocosSharp;

namespace tests
{
    public enum KTag
    {
        kTagNode,
        kTagGrossini,
        kTagSequence,
    }
    public class RemoveTest : ActionManagerTest
    {
        int kTagGrossini = 1;
        string s_pPathGrossini = "Images/grossini";

        public override string title() 
        {
            return "Remove Test";
        }

        public override void OnEnter()
        {
            base.OnEnter();

            CCSize s = Layer.VisibleBoundsWorldspace.Size;

            CCLabelTtf l = new CCLabelTtf("Should not crash", "arial", 16);
            AddChild(l);
            l.Position = (new CCPoint(s.Width / 2, 245));

            CCMoveBy pMove = new CCMoveBy (2, new CCPoint(200, 0));
            CCCallFunc pCallback = new CCCallFunc(stopAction);
            CCFiniteTimeAction pSequence = (CCFiniteTimeAction)new CCSequence(pMove, pCallback);
            pSequence.Tag = (int)KTag.kTagSequence;

            CCSprite pChild = new CCSprite(s_pPathGrossini);
            pChild.Position = (new CCPoint(200, 200));

            AddChild(pChild, 1, kTagGrossini);
            pChild.RunAction(pSequence);
        }

        public void stopAction()
        {
            CCNode pSprite = GetChildByTag(kTagGrossini);
            pSprite.StopAction((int)KTag.kTagSequence);
        }
    }
}
