using ArxOne.MrAdvice.Advice;
using IronworksTranslator.Models.Settings;

namespace IronworksTranslator.Utils.Aspect
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class SaveSettingsOnChange : Attribute, IMethodAdvice
    {
        public void Advise(MethodAdviceContext context)
        {
            context.Proceed();
            if (IronworksSettings.Instance != null)
            {
                IronworksSettings.UpdateSettingsFile(IronworksSettings.Instance);
            }
        }
    }
}
