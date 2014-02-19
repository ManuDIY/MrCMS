using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Web.Routing;
using MrCMS.Entities.Documents;
using MrCMS.Entities.Documents.Layout;
using MrCMS.Entities.Documents.Media;
using MrCMS.Entities.Documents.Web;
using MrCMS.Entities.Multisite;
using MrCMS.Entities.People;
using MrCMS.Entities.Widget;
using MrCMS.Helpers;
using MrCMS.Models;
using MrCMS.Settings;
using MrCMS.Website;
using NHibernate;
using NHibernate.Criterion;

namespace MrCMS.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly ISession _session;
        private readonly IDocumentEventService _documentEventService;
        private readonly SiteSettings _siteSettings;
        private readonly Site _currentSite;

        public DocumentService(ISession session, IDocumentEventService documentEventService, SiteSettings siteSettings, Site currentSite)
        {
            _session = session;
            _documentEventService = documentEventService;
            _siteSettings = siteSettings;
            _currentSite = currentSite;
        }

        public void AddDocument<T>(T document) where T : Document
        {
            _session.Transact(session =>
                                  {
                                      document.DisplayOrder = GetMaxParentDisplayOrder(document);
                                      document.CustomInitialization(this, _session);
                                      if (document.Parent != null)
                                          document.Parent.Children.Add(document);
                                      session.SaveOrUpdate(document);
                                      
                                  });
            _documentEventService.OnDocumentAdded(document);
        }

        private int GetMaxParentDisplayOrder(Document document)
        {
            if (document.Parent != null)
            {
                var enumerable = document.Parent.Children.Where(d => d != document).ToList();
                return enumerable.Any()
                           ? enumerable.Max(d => d.DisplayOrder) + 1
                           : 0;
            }
            if (document is MediaCategory)
            {
                var documentsByParent = GetDocumentsByParent<MediaCategory>(null).ToList();
                return documentsByParent.Any()
                           ? documentsByParent.Max(category => category.DisplayOrder) + 1
                           : 0;
            }
            else if (document is Layout)
            {
                var documentsByParent = GetDocumentsByParent<Layout>(null).ToList();
                return documentsByParent.Any()
                           ? documentsByParent.Max(category => category.DisplayOrder) + 1
                           : 0;
            }
            else
            {
                var documentsByParent = GetDocumentsByParent<Webpage>(null).ToList();
                return documentsByParent.Any()
                           ? documentsByParent.Max(category => category.DisplayOrder) + 1
                           : 0;
            }
        }

        public T GetDocument<T>(int id) where T : Document
        {
            return _session.Get<T>(id);
        }
        public T SaveDocument<T>(T document) where T : Document
        {
            _session.Transact(session =>
            {
                document.OnSaving(session);
                session.SaveOrUpdate(document);
            });
            return document;
        }

        public IEnumerable<T> GetAllDocuments<T>() where T : Document
        {
            return _session.QueryOver<T>().Where(arg => arg.Site == _currentSite).Cacheable().List();
        }

        public bool ExistAny(Type type)
        {
            var uniqueResult =
                _session.CreateCriteria(type)
                        .Add(Restrictions.Eq(Projections.Property("Site.Id"), _currentSite.Id))
                        .SetProjection(Projections.RowCount()).SetCacheable(true)
                        .UniqueResult<int>();
            return uniqueResult != 0;
        }

        public IEnumerable<T> GetDocumentsByParent<T>(T parent) where T : Document
        {
            IEnumerable<T> list = parent != null
                ? parent.Children.OfType<T>()
                : _session.QueryOver<T>()
                    .Where(arg => arg.Parent == null && arg.Site.Id == _currentSite.Id)
                    .OrderBy(arg => arg.DisplayOrder)
                    .Asc.Cacheable()
                    .List();
            return list;
        }

        public string GetDocumentUrl(string pageName, Webpage parent, bool useHierarchy = false)
        {
            var stringBuilder = new StringBuilder();

            if (useHierarchy)
            {
                //get breadcrumb from parent
                if (parent != null)
                {
                    stringBuilder.Insert(0, SeoHelper.TidyUrl(parent.UrlSegment) + "/");
                }
            }
            //add page name

            stringBuilder.Append(SeoHelper.TidyUrl(pageName));

            //make sure the URL is unique

            if (!UrlIsValidForWebpage(stringBuilder.ToString(), null))
            {
                var counter = 1;

                while (!UrlIsValidForWebpage(string.Format("{0}-{1}", stringBuilder, counter), null))
                    counter++;

                stringBuilder.AppendFormat("-{0}", counter);
            }
            return stringBuilder.ToString();
        }

        public Layout GetDefaultLayout(Webpage currentPage)
        {
            if (currentPage != null)
            {
                string defaultLayoutName = currentPage.GetMetadata().DefaultLayoutName;
                if (!String.IsNullOrEmpty(defaultLayoutName))
                {
                    var layout =
                        _session.QueryOver<Layout>()
                                .Where(x => x.Name == defaultLayoutName)
                                .Cacheable()
                                .List()
                                .FirstOrDefault();
                    if (layout != null)
                        return layout;
                }
            }
            var settingValue = _siteSettings.DefaultLayoutId;

            return _session.Get<Layout>(settingValue) ??
                   _session.QueryOver<Layout>()
                           .Where(layout => layout.Site == currentPage.Site)
                           .Take(1)
                           .Cacheable()
                           .SingleOrDefault();
        }

        public void SetTags(string taglist, Document document)
        {
            if (document == null) throw new ArgumentNullException("document");

            if (taglist == null)
                taglist = string.Empty;

            var tagNames =
                taglist.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(
                    x => !string.IsNullOrWhiteSpace(x));

            var existingTags = document.Tags.ToList();

            tagNames.ForEach(name =>
            {
                var tag = GetTag(name) ?? new Tag { Name = name };
                if (!document.Tags.Contains(tag))
                {
                    document.Tags.Add(tag);
                    tag.Documents.Add(document);
                }
                existingTags.Remove(tag);
            });

            existingTags.ForEach(tag =>
            {
                document.Tags.Remove(tag);
                tag.Documents.Remove(document);
            });
        }

        private Tag GetTag(string name)
        {
            return _session.QueryOver<Tag>().Where(tag => tag.Name.IsLike(name, MatchMode.Exact)).Take(1).SingleOrDefault();
        }

        public void SetOrder(int documentId, int order)
        {
            _session.Transact(session =>
            {
                var document = session.Get<Document>(documentId);
                document.DisplayOrder = order;
                session.SaveOrUpdate(document);
            });
        }

        public void SetOrders(List<SortItem> items)
        {
            _session.Transact(session => items.ForEach(item =>
            {
                var document = session.Get<Document>(item.Id);
                document.DisplayOrder = item.Order;
                session.Update(document);
            }));
        }

        public bool AnyPublishedWebpages()
        {
            return _session.QueryOver<Webpage>().Where(webpage => webpage.PublishOn != null && webpage.PublishOn <= DateTime.Now).Cacheable().RowCount() > 0;
        }

        public bool AnyWebpages()
        {
            return _session.QueryOver<Webpage>().Cacheable().RowCount() > 0;
        }

        public IEnumerable<Webpage> GetWebPagesByParentIdForNav(int parentId)
        {
            return
                _session.QueryOver<Webpage>().Where(
                    x => x.Parent.Id == parentId && x.PublishOn != null && x.PublishOn <= DateTime.Now && x.PublishOn < DateTime.Now && x.RevealInNavigation).
                    List();
        }

        public void DeleteDocument<T>(T document) where T : Document
        {
            if (document != null)
            {
                _session.Transact(session =>
                {
                    document.OnDeleting(session);
                    session.Delete(document);
                });
                _documentEventService.OnDocumentDeleted(document);
            }
        }

        public void PublishNow(Webpage document)
        {
            if (document.PublishOn == null)
            {
                document.PublishOn = CurrentRequestData.Now;
                SaveDocument(document);
            }
        }

        public void Unpublish(Webpage document)
        {
            document.PublishOn = null;
            SaveDocument(document);
            _documentEventService.OnDocumentUnpublished(document);
        }

        public void HideWidget(Webpage document, int widgetId)
        {
            var widget = _session.Get<Widget>(widgetId);

            if (document == null || widget == null) return;

            if (document.ShownWidgets.Contains(widget))
                document.ShownWidgets.Remove(widget);
            else if (!document.HiddenWidgets.Contains(widget))
                document.HiddenWidgets.Add(widget);
            SaveDocument(document);
        }

        public void ShowWidget(Webpage document, int widgetId)
        {
            var widget = _session.Get<Widget>(widgetId);

            if (document == null || widget == null) return;

            if (document.HiddenWidgets.Contains(widget))
                document.HiddenWidgets.Remove(widget);
            else if (!document.ShownWidgets.Contains(widget))
                document.ShownWidgets.Add(widget);
            SaveDocument(document);

        }


        public DocumentVersion GetDocumentVersion(int id)
        {
            return _session.Get<DocumentVersion>(id);
        }

        public void SetParent(Document document, int? parentId)
        {
            if (document == null) return;

            var parent = parentId.HasValue ? GetDocument<Webpage>(parentId.Value) : null;

            document.SetParent(parent);

            SaveDocument(document);
        }

        public DocumentMetadata GetDefinitionByType(Type type)
        {
            return DocumentMetadataHelper.GetMetadataByType(type);
        }

        public void SetFrontEndRoles(string frontEndRoles, Webpage webpage)
        {
            if (webpage == null) throw new ArgumentNullException("webpage");

            if (frontEndRoles == null)
                frontEndRoles = string.Empty;

            var roleNames =
                frontEndRoles.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(
                    x => !string.IsNullOrWhiteSpace(x));

            var roles = webpage.FrontEndAllowedRoles.ToList();

            if (webpage.InheritFrontEndRolesFromParent)
            {
                roles.ForEach(role =>
                {
                    role.FrontEndWebpages.Remove(webpage);
                    webpage.FrontEndAllowedRoles.Remove(role);
                });
            }
            else
            {
                roleNames.ForEach(name =>
                {
                    var role = GetRole(name);
                    if (role != null)
                    {
                        if (!webpage.FrontEndAllowedRoles.Contains(role))
                        {
                            webpage.FrontEndAllowedRoles.Add(role);
                            role.FrontEndWebpages.Add(webpage);
                        }
                        roles.Remove(role);
                    }

                });

                roles.ForEach(role =>
                {
                    webpage.FrontEndAllowedRoles.Remove(role);
                    role.FrontEndWebpages.Remove(webpage);
                });
            }

        }

        private UserRole GetRole(string name)
        {
            return _session.QueryOver<UserRole>().Where(role => role.Name.IsInsensitiveLike(name, MatchMode.Exact)).SingleOrDefault();
        }

        public T GetDocumentByUrl<T>(string url) where T : Document
        {
            return _session.QueryOver<T>()
                        .Where(doc => doc.UrlSegment == url && doc.Site.Id == _currentSite.Id)
                        .Take(1).Cacheable()
                        .SingleOrDefault();
        }

        public bool UrlIsValidForWebpage(string url, int? id)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            if (id.HasValue)
            {
                var document = GetDocument<Webpage>(id.Value);
                var documentHistory = document.Urls.Any(x => x.UrlSegment == url);
                if (url.Trim() == document.UrlSegment.Trim() || documentHistory) //if url is the same or has been used for the same page before lets go
                    return true;

                return !WebpageExists(url) && !ExistsInUrlHistory(url);
            }

            return !WebpageExists(url) && !ExistsInUrlHistory(url);
        }

        /// <summary>
        /// Check to see if the supplied URL is ok to be added to the URL history table
        /// </summary>
        public bool UrlIsValidForWebpageUrlHistory(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            return !WebpageExists(url) && !ExistsInUrlHistory(url);
        }

        public IEnumerable<SelectListItem> GetValidParents(Webpage webpage)
        {
            var validParentTypes = DocumentMetadataHelper.GetValidParentTypes(webpage);
            var potentialParents = new List<Webpage>();

            foreach (var metadata in validParentTypes)
            {
                potentialParents.AddRange(_session.CreateCriteria(metadata.Type).SetCacheable(true).List<Webpage>());
            }

            var result = potentialParents.Distinct().Where(page => !page.ActivePages.Contains(webpage) && page.Site.Id == _currentSite.Id).OrderBy(x => x.Name)
                                                        .BuildSelectItemList(page => string.Format("{0} ({1})", page.Name, page.GetMetadata().Name),
                                                                             page => page.Id.ToString(), emptyItem: null);

            if (!webpage.GetMetadata().RequiresParent)
                result.Insert(0, SelectListItemHelper.EmptyItem("Root"));

            return result;
        }

        public IEnumerable<Document> GetParents(int? parent)
        {
            if (parent > 0)
            {
                var document = _session.Get<Document>(parent);
                while (document != null)
                {
                    yield return document;
                    document = document.Parent;
                }
            }
        }

        public Webpage GetHomePage()
        {
            return
                _session.QueryOver<Webpage>()
                        .Where(
                            document =>
                            document.Site.Id == _currentSite.Id && document.Parent == null)
                        .OrderBy(webpage => webpage.DisplayOrder).Asc
                        .Cacheable().List()
                        .FirstOrDefault(webpage => webpage.Published);
        }

        public void RevertToVersion(DocumentVersion documentVersion)
        {
            var currentVersion = documentVersion.Document;
            var previousVersion = currentVersion.GetVersion(documentVersion.Id);

            var versionProperties = currentVersion.GetType().GetVersionProperties();
            foreach (var versionProperty in versionProperties)
            {
                versionProperty.SetValue(currentVersion, versionProperty.GetValue(previousVersion, null), null);
            }
            _session.Transact(session => session.Update(currentVersion));
        }

        public bool UrlIsValidForMediaCategory(string url, int? id)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            if (id.HasValue)
            {
                var document = GetDocument<MediaCategory>(id.Value);
                if (url.Trim() == document.UrlSegment.Trim())
                    return true;
                return !MediaCategoryExists(url);
            }

            return !MediaCategoryExists(url);
        }

        public bool UrlIsValidForLayout(string url, int? id)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            if (id.HasValue)
            {
                var document = GetDocument<Layout>(id.Value);
                if (url.Trim() == document.UrlSegment.Trim())
                    return true;
                return !LayoutExists(url);
            }

            return !LayoutExists(url);
        }

        public UrlHistory GetHistoryItemByUrl(string url)
        {
            return _session.QueryOver<UrlHistory>()
                        .Where(doc => doc.UrlSegment == url && doc.Site.Id == _currentSite.Id)
                        .Take(1).Cacheable()
                        .SingleOrDefault();
        }

        /// <summary>
        /// Checks to see if the supplied url is unique for media category / folder.
        /// </summary>
        /// <param name="url"></param>
        /// <returns>bool</returns>
        private bool MediaCategoryExists(string url)
        {
            return _session.QueryOver<MediaCategory>()
                        .Where(doc => doc.UrlSegment == url && doc.Site.Id == _currentSite.Id)
                        .Cacheable()
                        .RowCount() > 0;
        }

        /// <summary>
        /// Checks to see if the supplied url is unique for layouts
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private bool LayoutExists(string url)
        {
            return _session.QueryOver<Layout>()
                        .Where(doc => doc.UrlSegment == url && doc.Site.Id == _currentSite.Id)
                        .Cacheable()
                        .RowCount() > 0;
        }

        /// <summary>
        /// Checks to see if a webpage exists with the supplied URL
        /// </summary>
        /// <param name="url"></param>
        /// <returns>bool</returns>
        private bool WebpageExists(string url)
        {
            return _session.QueryOver<Webpage>()
                        .Where(doc => doc.UrlSegment == url && doc.Site.Id == _currentSite.Id)
                        .Cacheable()
                        .RowCount() > 0;
        }

        /// <summary>
        /// Checks to see if the supplied URL exists in webpage URL history table
        /// </summary>
        /// <param name="url"></param>
        /// <returns>bool</returns>
        private bool ExistsInUrlHistory(string url)
        {
            return
                _session.QueryOver<UrlHistory>()
                        .Where(doc => doc.UrlSegment == url && doc.Site.Id == _currentSite.Id)
                        .Cacheable()
                        .RowCount() > 0;
        }


    }
}