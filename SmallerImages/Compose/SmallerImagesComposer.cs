using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;

namespace SmallerImages.Compose
{
    [RuntimeLevel(MinLevel = RuntimeLevel.Run)]
    public class SmallerImagesComposer : IUserComposer
    {
        public void Compose(Composition composition)
        {
            composition.Components().Append<SmallerImagesComponent>();
        }
    }
}
