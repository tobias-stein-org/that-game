using UnityEngine.UIElements;

namespace tg.editor.ai.behaviour.tree
{
    public class SplitView : TwoPaneSplitView
    {
        public new class UxmlFactory : UxmlFactory<SplitView, TwoPaneSplitView.UxmlTraits>
        { }
    }
}
