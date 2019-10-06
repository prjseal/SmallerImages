using Umbraco.Core;
using Umbraco.Core.Composing;

namespace SmallerImages.Compose
{
    [RuntimeLevel(MinLevel = RuntimeLevel.Run)]
    public class MediaInfoComposer : IUserComposer
    {
        public void Compose(Composition composition)
        {
            composition.Components().Append<SmallerImagesComponent>();
        }
    }
}
