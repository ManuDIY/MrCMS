using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MrCMS.Helpers;
using MrCMS.Messages;

namespace MrCMS.Web.Apps.Admin.ModelBinders
{
    public class MessageTemplateOverrideModelBinder : IModelBinder
    {
        //public MessageTemplateOverrideModelBinder(IKernel kernel)
        //    : base(kernel)
        //{
        //}

        //public override object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        //{
        //    var type = GetTypeByName(controllerContext);

        //    bindingContext.ModelMetadata =
        //        ModelMetadataProviders.Current.GetMetadataForType(
        //            () => CreateModel(controllerContext, bindingContext, type), type);

        //    var messageTemplate = base.BindModel(controllerContext, bindingContext) as MessageTemplate;

        //    return messageTemplate;
        //}

        //protected override object CreateModel(ControllerContext controllerContext, ModelBindingContext bindingContext, Type modelType)
        //{
        //    var type = GetTypeByName(controllerContext);
        //    return Activator.CreateInstance(type);
        //}

        private static Type GetTypeByName(ModelBindingContext modelBindingContext)
        {
            var valueFromContext = modelBindingContext.ValueProvider.GetValue("TemplateType");
            return TypeHelper.GetTypeByName(valueFromContext.FirstValue);
        }
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var type = GetTypeByName(bindingContext);

            var serviceProvider = bindingContext.HttpContext.RequestServices;
            var metadataProvider = serviceProvider.GetRequiredService<IModelMetadataProvider>();
            var metadata = metadataProvider.GetMetadataForType(type);
            bindingContext.ModelMetadata = metadata;
            var binderFactory= serviceProvider.GetRequiredService<IModelBinderFactory>();

            var modelBinder = binderFactory.CreateBinder(new ModelBinderFactoryContext
            {
                Metadata = metadata,
                BindingInfo = BindingInfo.GetBindingInfo(Enumerable.Empty<object>(), metadata),
            });
            return modelBinder.BindModelAsync(bindingContext);
        }
    }
}