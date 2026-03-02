using System.Collections.Generic;

namespace PACK.Models
{
    public enum SeqType { Click, Key, Wait, Move }

    public class SequenceItem
    {
        public SeqType     Type          { get; set; }
        // Click
        public int?        ClickX        { get; set; }
        public int?        ClickY        { get; set; }
        public bool        WindowCenter  { get; set; }
        // Key
        public List<int>?  VkList        { get; set; }
        public string?     ComboLabel    { get; set; }
        // Wait
        public int         WaitMs        { get; set; }

        public string Display => Type switch
        {
            SeqType.Click when WindowCenter                         => "Click at target window center",
            SeqType.Click when ComboLabel == "right"               => $"Right-Click at {ClickX},{ClickY}",
            SeqType.Click                                           => $"Click at {ClickX},{ClickY}",
            SeqType.Key                                             => $"Key combo: {ComboLabel}",
            SeqType.Wait                                            => $"Wait {WaitMs} ms",
            SeqType.Move                                            => $"Move to {ClickX},{ClickY}",
            _                                                       => "?"
        };
    }
}
