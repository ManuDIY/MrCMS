﻿using MrCMS.Web.Areas.Admin.Models;

namespace MrCMS.Web.Areas.Admin.Services
{
    public interface ICreateMergeBatch
    {
        bool CreateBatch(MergeWebpageConfirmationModel model);
    }
}