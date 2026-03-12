using System;
using System.Collections.Generic;
using System.Text;

namespace LockBox.Subviews;

[Flags]
internal enum RightSideViews
{
    None = 1,
    CurrentlyOpenBox = 2,
    OpenBoxControls = 4,
    BoxIsOpen = CurrentlyOpenBox | OpenBoxControls,
    BoxIsClosed = None
}
