﻿using System;
using RazorEngine.Templating;

namespace FubuMVC.Razor.RazorEngine
{
    public interface IRazorViewEntry
    {
        Guid ViewId { get; }
        RazorViewDescriptor Descriptor { get; }
        ITemplate CreateInstance();
        void ReleaseInstance(ITemplate view);
        string SourceCode { get; }
        bool IsCurrent();
    }
}