﻿using System;
using System.Collections.Generic;
using System.Linq;
using FakeItEasy;
using MrCMS.Entities.Documents;
using MrCMS.Entities.Documents.Layout;
using MrCMS.Entities.Documents.Web;
using MrCMS.Entities.Widget;
using MrCMS.Helpers;
using MrCMS.Services;
using MrCMS.Settings;
using NHibernate;
using Xunit;
using FluentAssertions;

namespace MrCMS.Tests.Services
{
    public class DocumentServiceTests : InMemoryDatabaseTest
    {
        private readonly SiteSettings _siteSettings;

        public DocumentServiceTests()
        {
            _siteSettings = new SiteSettings();
        }
        [Fact]
        public void AddDocument_OnSave_AddsToRepository()
        {
            var documentService = GetDocumentService();

            documentService.AddDocument(new TextPage());

            Session.QueryOver<Document>().RowCount().Should().Be(1);
        }

        private DocumentService GetDocumentService()
        {
            var documentService = new DocumentService(Session, _siteSettings);
            return documentService;
        }

        [Fact]
        public void GetDocument_WhenDocumentDoesNotExist_ReturnsNull()
        {
            var documentService = GetDocumentService();

            var document = documentService.GetDocument<TextPage>(1);

            document.Should().BeNull();
        }

        [Fact]
        public void DocumentService_SaveDocument_ReturnsTheSameDocument()
        {
            var documentService = GetDocumentService();

            var document = new TextPage();
            var updatedDocument = documentService.SaveDocument(document);

            document.Should().BeSameAs(updatedDocument);
        }

        [Fact]
        public void DocumentService_GetAllDocuments_ShouldReturnAListOfAllDocumentsOfTheSpecifiedType()
        {
            var documentService = GetDocumentService();

            Enumerable.Range(1, 10).ForEach(i => Session.Transact(session => session.SaveOrUpdate(new TextPage { Name = "Page " + i })));

            var allDocuments = documentService.GetAllDocuments<TextPage>();

            allDocuments.Should().HaveCount(10);
        }

        [Fact]
        public void DocumentService_GetAllDocuments_ShouldOnlyReturnDocumentsOfSpecifiedType()
        {
            var documentService = GetDocumentService();

            Enumerable.Range(1, 10).ForEach(i =>
                                         Session.Transact(
                                             session =>
                                             session.SaveOrUpdate(i % 2 == 0
                                                                      ? (Document)new TextPage { Name = "Page " + i }
                                                                      : new Layout { Name = "Layout " + i }
                                                 )));

            var allDocuments = documentService.GetAllDocuments<TextPage>();

            allDocuments.Should().HaveCount(5);
        }

        [Fact]
        public void DocumentService_GetDocumentsByParentId_ShouldReturnAllDocumentsThatHaveTheCorrespondingParentId()
        {
            var documentService = GetDocumentService();

            var parent = new TextPage
            {
                Name = "Parent",
                AdminAllowedRoles = new List<AdminAllowedRole>(),
                AdminDisallowedRoles = new List<AdminDisallowedRole>()
            };
            Session.Transact(session => session.SaveOrUpdate(parent));

            Enumerable.Range(1, 10).ForEach(i =>
                                             {
                                                 var textPage = new TextPage
                                                 {
                                                     Name = String.Format("Page {0}", (object)i),
                                                     Parent = parent,
                                                     AdminAllowedRoles = new List<AdminAllowedRole>(),
                                                     AdminDisallowedRoles = new List<AdminDisallowedRole>()
                                                 };
                                                 parent.Children.Add(textPage);
                                                 Session.Transact(session => session.SaveOrUpdate(textPage));
                                                 Session.Transact(session => session.SaveOrUpdate(parent));
                                             });

            var documents = documentService.GetAdminDocumentsByParentId<TextPage>(parent.Id);

            documents.Should().HaveCount(10);
        }

        [Fact]
        public void DocumentService_GetDocumentsByParentId_ShouldOnlyReturnRequestedType()
        {
            var documentService = GetDocumentService();

            var parent = new TextPage
                             {
                                 Name = "Parent",
                                 AdminAllowedRoles = new List<AdminAllowedRole>(),
                                 AdminDisallowedRoles = new List<AdminDisallowedRole>()
                             };
            Session.Transact(session => session.SaveOrUpdate(parent));

            Enumerable.Range(1, 10).ForEach(i =>
                                             {
                                                 var textPage = i % 2 == 0
                                                                    ? (Document)
                                                                      new TextPage
                                                                      {
                                                                          Name = String.Format("Page {0}", i),
                                                                          Parent = parent,
                                                                          AdminAllowedRoles = new List<AdminAllowedRole>(),
                                                                          AdminDisallowedRoles = new List<AdminDisallowedRole>()
                                                                      }
                                                                    : new Layout { Parent = parent };
                                                 parent.Children.Add(textPage);
                                                 Session.Transact(session => session.SaveOrUpdate(textPage));
                                                 Session.Transact(session => session.SaveOrUpdate(parent));
                                             });

            var textPages = documentService.GetAdminDocumentsByParentId<TextPage>(parent.Id);

            textPages.Should().HaveCount(5);
        }

        [Fact]
        public void DocumentService_GetDocumentsByParentId_ShouldOrderByDisplayOrder()
        {
            var documentService = GetDocumentService();

            var parent = new TextPage
                             {
                                 Name = "Parent",
                                 AdminAllowedRoles = new List<AdminAllowedRole>(),
                                 AdminDisallowedRoles = new List<AdminDisallowedRole>()
                             };
            Session.Transact(session => session.SaveOrUpdate(parent));

            Enumerable.Range(1, 3).ForEach(i =>
                                             {
                                                 var textPage = new TextPage
                                                 {
                                                     Name = String.Format("Page {0}", i),
                                                     Parent = parent,
                                                     DisplayOrder = 4 - i,
                                                     AdminAllowedRoles = new List<AdminAllowedRole>(),
                                                     AdminDisallowedRoles = new List<AdminDisallowedRole>()
                                                 };
                                                 parent.Children.Add(textPage);
                                                 Session.Transact(session => session.SaveOrUpdate(textPage));
                                             });

            var documents = documentService.GetAdminDocumentsByParentId<TextPage>(parent.Id).ToList();

            documents[0].DisplayOrder.Should().Be(1);
            documents[1].DisplayOrder.Should().Be(2);
            documents[2].DisplayOrder.Should().Be(3);
        }

        [Fact]
        public void DocumentService_GetDocumentByUrl_ReturnsTheDocumentWithTheSpecifiedUrl()
        {
            var documentService = GetDocumentService();

            var textPage = new TextPage { UrlSegment = "test-page" };
            Session.Transact(session => session.SaveOrUpdate(textPage));

            var document = documentService.GetDocumentByUrl<TextPage>("test-page");

            document.Should().NotBeNull();
        }

        [Fact]
        public void DocumentService_GetDocumentByUrl_ShouldReturnNullIfTheRequestedTypeDoesNotMatch()
        {
            var documentService = GetDocumentService();

            var textPage = new TextPage { UrlSegment = "test-page" };
            Session.Transact(session => session.SaveOrUpdate(textPage));

            var document = documentService.GetDocumentByUrl<Layout>("test-page");

            document.Should().BeNull();
        }

        [Fact]
        public void DocumentService_GetDocumentUrl_ReturnsAUrlBasedOnTheHierarchyIfTheFlagIsSetToTrue()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage { Name = "Test Page", UrlSegment = "test-page" };

            Session.Transact(session => session.SaveOrUpdate(textPage));

            var documentUrl = documentService.GetDocumentUrl("Nested Page", textPage.Id, true);

            documentUrl.Should().Be("test-page/nested-page");
        }
        [Fact]
        public void DocumentService_GetDocumentUrl_ReturnsAUrlBasedOnTheNameIfTheFlagIsSetToFalse()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage { Name = "Test Page", UrlSegment = "test-page" };

            Session.Transact(session => session.SaveOrUpdate(textPage));

            var documentUrl = documentService.GetDocumentUrl("Nested Page", textPage.Id, false);

            documentUrl.Should().Be("nested-page");
        }

        [Fact]
        public void DocumentService_GetDocumentUrlWithExistingName_ShouldReturnTheUrlWithADigitAppended()
        {
            var documentService = GetDocumentService();
            var parent = new TextPage { Name = "Parent", UrlSegment = "parent" };
            var textPage = new TextPage { Name = "Test Page", Parent = parent, UrlSegment = "parent/test-page" };
            var existingPage = new TextPage
                                   {
                                       Name = "Nested Page",
                                       UrlSegment = "parent/test-page/nested-page",
                                       Parent = textPage
                                   };
            Session.Transact(session =>
            {
                session.SaveOrUpdate(parent);
                session.SaveOrUpdate(textPage);
                session.SaveOrUpdate(existingPage);
            });

            var documentUrl = documentService.GetDocumentUrl("Nested Page", textPage.Id, true);

            documentUrl.Should().Be("parent/test-page/nested-page-1");
        }

        [Fact]
        public void DocumentService_GetDocumentUrlWithExistingName_MultipleFilesWithSameNameShouldNotAppendMultipleDigits()
        {
            var documentService = GetDocumentService();
            var parent = new TextPage { Name = "Parent", UrlSegment = "parent" };
            var textPage = new TextPage { Name = "Test Page", Parent = parent, UrlSegment = "parent/test-page" };
            var existingPage = new TextPage
                                   {
                                       Name = "Nested Page",
                                       UrlSegment = "parent/test-page/nested-page",
                                       Parent = textPage
                                   };
            var existingPage2 = new TextPage
                                   {
                                       Name = "Nested Page",
                                       UrlSegment = "parent/test-page/nested-page-1",
                                       Parent = textPage
                                   };
            Session.Transact(session =>
            {
                session.SaveOrUpdate(parent);
                session.SaveOrUpdate(textPage);
                session.SaveOrUpdate(existingPage);
                session.SaveOrUpdate(existingPage2);
            });

            var documentUrl = documentService.GetDocumentUrl("Nested Page", textPage.Id, true);

            documentUrl.Should().Be("parent/test-page/nested-page-2");
        }

        [Fact]
        public void DocumentService_SetTags_IfDocumentIsNullThrowArgumentNullException()
        {
            var documentService = GetDocumentService();

            documentService.Invoking(service => service.SetTags(null, null)).ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void DocumentService_SetTags_IfTagsIsNullForANewDocumentTheTagListShouldBeEmpty()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();

            documentService.SetTags(null, textPage);

            textPage.Tags.Should().HaveCount(0);
        }

        [Fact]
        public void DocumentService_SetTags_IfTagsHasOneStringTheTagListShouldHave1Tag()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();

            documentService.SetTags("test tag", textPage);

            textPage.Tags.Should().HaveCount(1);
        }

        [Fact]
        public void DocumentService_SetTags_IfTagsHasTwoCommaSeparatedTagsTheTagListShouldHave2Tags()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();

            documentService.SetTags("test 1, test 2", textPage);

            textPage.Tags.Should().HaveCount(2);
        }

        [Fact]
        public void DocumentService_SetTags_ShouldTrimTagNames()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();

            documentService.SetTags("test 1, test 2", textPage);

            textPage.Tags[1].Name.Should().Be("test 2");
        }

        [Fact]
        public void DocumentService_SetTags_ShouldSaveGeneratedTags()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();

            documentService.SetTags("test 1, test 2", textPage);

            Session.QueryOver<Tag>().RowCount().Should().Be(2);
        }

        [Fact]
        public void DocumentService_SetTags_ShouldNotRecreateTags()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();
            var tag1 = new Tag { Name = "test 1" };
            var tag2 = new Tag { Name = "test 2" };
            textPage.Tags.Add(tag1);
            textPage.Tags.Add(tag2);

            Session.Transact(session =>
                                 {
                                     session.SaveOrUpdate(textPage);
                                     session.SaveOrUpdate(tag1);
                                     session.SaveOrUpdate(tag2);
                                 });

            documentService.SetTags(textPage.TagList, textPage);

            Session.QueryOver<Tag>().RowCount().Should().Be(2);
        }
        [Fact]
        public void DocumentService_SetTags_ShouldNotReaddSetTags()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();
            var tag1 = new Tag { Name = "test 1" };
            var tag2 = new Tag { Name = "test 2" };
            textPage.Tags.Add(tag1);
            textPage.Tags.Add(tag2);

            Session.Transact(session =>
            {
                session.SaveOrUpdate(textPage);
                session.SaveOrUpdate(tag1);
                session.SaveOrUpdate(tag2);
            });

            documentService.SetTags(textPage.TagList, textPage);

            textPage.Tags.Should().HaveCount(2);
        }

        [Fact]
        public void DocumentService_SetTags_ShouldRemoveTagsNotIncluded()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();
            var tag1 = new Tag { Name = "test 1" };
            var tag2 = new Tag { Name = "test 2" };
            textPage.Tags.Add(tag1);
            textPage.Tags.Add(tag2);

            Session.Transact(session =>
                                 {
                                     session.SaveOrUpdate(textPage);
                                     session.SaveOrUpdate(tag1);
                                     session.SaveOrUpdate(tag2);
                                 });

            documentService.SetTags("test 1", textPage);

            textPage.Tags.Should().HaveCount(1);
        }

        [Fact]
        public void DocumentService_SetTags_ShouldAssignDocumentToTag()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();

            Session.Transact(session => session.SaveOrUpdate(textPage));

            documentService.SetTags("test 1", textPage);

            var tags = Session.QueryOver<Tag>().List();

            tags.Should().HaveCount(1);
            tags.First().Documents.Should().HaveCount(1);
        }

        [Fact]
        public void DocumentService_SetTags_ShouldRemoveTheDocumentFromTags()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();
            var tag1 = new Tag { Name = "test 1" };
            var tag2 = new Tag { Name = "test 2" };
            textPage.Tags.Add(tag1);
            textPage.Tags.Add(tag2);
            tag1.Documents.Add(textPage);
            tag2.Documents.Add(textPage);

            Session.Transact(session =>
                                 {
                                     session.SaveOrUpdate(textPage);
                                     session.SaveOrUpdate(tag1);
                                     session.SaveOrUpdate(tag2);
                                 });

            documentService.SetTags("test 1", textPage);

            tag1.Documents.Should().HaveCount(1);
            tag2.Documents.Should().HaveCount(0);
        }

        [Fact]
        public void DocumentService_SetTags_ShouldNotCreateTagsWithEmptyNames()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();

            documentService.SetTags("test 1,,test 2", textPage);

            textPage.Tags.Should().HaveCount(2);
            Session.QueryOver<Tag>().List().Should().HaveCount(2);
        }

        [Fact]
        public void DocumentService_SetTags_ShouldNotCreateTagsWithEmptyNamesForTrailingComma()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();

            documentService.SetTags("test 1, test 2, ", textPage);

            textPage.Tags.Should().HaveCount(2);
            Session.QueryOver<Tag>().List().Should().HaveCount(2);
        }

        [Fact]
        public void DocumentService_SetOrder_ShouldSetTheDocumentOrderOfTheDocumentWithTheSetId()
        {
            var documentService = GetDocumentService();
            var textPage = new TextPage();

            Session.Transact(session => session.SaveOrUpdate(textPage));

            documentService.SetOrder(textPage.Id, 2);

            textPage.DisplayOrder.Should().Be(2);
        }

        [Fact]
        public void DocumentService_SearchDocuments_ReturnsAnIEnumerableOfSearchResultModelsWhereTheNameMatches()
        {
            var doc1 = new TextPage { Name = "Test" };
            var doc2 = new TextPage { Name = "Different Name" };
            Session.Transact(session =>
                                 {
                                     session.SaveOrUpdate(doc1);
                                     session.SaveOrUpdate(doc2);
                                 });
            var documentService = GetDocumentService();

            var searchResultModels = documentService.SearchDocuments<TextPage>("Test");

            searchResultModels.Should().HaveCount(1);
            searchResultModels.First().Name.Should().Be("Test");
        }

        [Fact]
        public void DocumentService_AnyWebpages_ReturnsFalseWhenNoWebpagesAreSaved()
        {
            var documentService = GetDocumentService();

            documentService.AnyWebpages().Should().BeFalse();
        }

        [Fact]
        public void DocumentService_AnyWebpages_ReturnsTrueOnceAWebpageIsAdded()
        {
            var documentService = GetDocumentService();

            documentService.AddDocument(new TextPage());

            documentService.AnyWebpages().Should().BeTrue();
        }

        [Fact]
        public void DocumentService_AnyPublishedWebpages_ReturnsFalseWhenThereAreNoWebpages()
        {
            var documentService = GetDocumentService();

            documentService.AnyPublishedWebpages().Should().BeFalse();
        }

        [Fact]
        public void DocumentService_AnyPublishedWebpages_ReturnsFalseWhenThereAreWebpagesButTheyAreNotPublished()
        {
            var documentService = GetDocumentService();

            documentService.AddDocument(new TextPage());

            documentService.AnyPublishedWebpages().Should().BeFalse();
        }

        [Fact]
        public void DocumentService_AnyPublishedWebpages_ReturnsTrueOnceAPublishedWebpageIsAdded()
        {
            var documentService = GetDocumentService();

            documentService.AddDocument(new TextPage { PublishOn = DateTime.UtcNow.AddDays(-1) });

            documentService.AnyPublishedWebpages().Should().BeTrue();
        }

        [Fact]
        public void DocumentService_HideWidget_AddsAWidgetToTheHiddenWidgetsListIfItIsNotInTheShownList()
        {
            var documentService = GetDocumentService();
            var widgetService = new WidgetService(Session);

            var textPage = new TextPage { ShownWidgets = new List<Widget>(), HiddenWidgets = new List<Widget>() };
            documentService.SaveDocument(textPage);

            var textWidget = new TextWidget();
            widgetService.SaveWidget(textWidget);

            documentService.HideWidget(textPage.Id, textWidget.Id);

            textPage.HiddenWidgets.Should().Contain(textWidget);
        }

        [Fact]
        public void DocumentService_HideWidget_RemovesAWidgetFromTheShownListIfItIsIncluded()
        {
            var documentService = GetDocumentService();
            var widgetService = new WidgetService(Session);

            var textWidget = new TextWidget();
            widgetService.SaveWidget(textWidget);

            var textPage = new TextPage
            {
                ShownWidgets = new List<Widget> { textWidget },
                HiddenWidgets = new List<Widget>()
            };
            documentService.SaveDocument(textPage);

            documentService.HideWidget(textPage.Id, textWidget.Id);

            textPage.ShownWidgets.Should().NotContain(textWidget);
        }

        [Fact]
        public void DocumentService_HideWidget_DoesNothingIfTheWidgetIdIsInvalid()
        {
            var documentService = GetDocumentService();
            var widgetService = new WidgetService(Session);

            var textWidget = new TextWidget();
            widgetService.SaveWidget(textWidget);

            var textPage = new TextPage
            {
                ShownWidgets = new List<Widget> { textWidget },
                HiddenWidgets = new List<Widget>()
            };
            documentService.SaveDocument(textPage);

            documentService.HideWidget(textPage.Id, -1);

            textPage.ShownWidgets.Should().Contain(textWidget);
        }


        [Fact]
        public void DocumentService_ShowWidget_AddsAWidgetToTheShownWidgetsListIfItIsNotInTheHiddenList()
        {
            var documentService = GetDocumentService();
            var widgetService = new WidgetService(Session);

            var textPage = new TextPage { ShownWidgets = new List<Widget>(), HiddenWidgets = new List<Widget>() };
            documentService.SaveDocument(textPage);

            var textWidget = new TextWidget();
            widgetService.SaveWidget(textWidget);

            documentService.ShowWidget(textPage.Id, textWidget.Id);

            textPage.ShownWidgets.Should().Contain(textWidget);
        }

        [Fact]
        public void DocumentService_ShowWidget_RemovesAWidgetFromTheHiddenListIfItIsIncluded()
        {
            var documentService = GetDocumentService();
            var widgetService = new WidgetService(Session);

            var textWidget = new TextWidget();
            widgetService.SaveWidget(textWidget);

            var textPage = new TextPage
            {
                ShownWidgets = new List<Widget>(),
                HiddenWidgets = new List<Widget> { textWidget }
            };
            documentService.SaveDocument(textPage);

            documentService.ShowWidget(textPage.Id, textWidget.Id);

            textPage.HiddenWidgets.Should().NotContain(textWidget);
        }

        [Fact]
        public void DocumentService_ShowWidget_DoesNothingIfTheWidgetIdIsInvalid()
        {
            var documentService = GetDocumentService();
            var widgetService = new WidgetService(Session);

            var textWidget = new TextWidget();
            widgetService.SaveWidget(textWidget);

            var textPage = new TextPage
            {
                ShownWidgets = new List<Widget>(),
                HiddenWidgets = new List<Widget> { textWidget }
            };
            documentService.SaveDocument(textPage);

            documentService.ShowWidget(textPage.Id, -1);

            textPage.HiddenWidgets.Should().Contain(textWidget);
        }

        [Fact]
        public void DocumentService_PublishNow_UnpublishedWebpageWillGetPublishedOnValue()
        {
            var documentService = GetDocumentService();

            var textPage = new TextPage();

            Session.Transact(session => session.Save(textPage));

            documentService.PublishNow(textPage);

            textPage.PublishOn.Should().HaveValue();
        }

        [Fact]
        public void DocumentService_PublishNow_PublishedWebpageShouldNotChangeValue()
        {
            var documentService = GetDocumentService();

            var publishOn = DateTime.Now.AddDays(-1);
            var textPage = new TextPage { PublishOn = publishOn };

            Session.Transact(session => session.Save(textPage));

            documentService.PublishNow(textPage);

            textPage.PublishOn.Should().Be(publishOn);
        }


        [Fact]
        public void DocumentService_Unpublish_ShouldSetPublishOnToNull()
        {
            var documentService = GetDocumentService();

            var publishOn = DateTime.Now.AddDays(-1);
            var textPage = new TextPage { PublishOn = publishOn };

            Session.Transact(session => session.Save(textPage));

            documentService.Unpublish(textPage);

            textPage.PublishOn.Should().NotHaveValue();
        }

        [Fact]
        public void DocumentService_DeleteDocument_ShouldCallSessionDelete()
        {
            var session = A.Fake<ISession>();
            var documentService = new DocumentService(session, new SiteSettings());

            var textPage = new TextPage();

            documentService.DeleteDocument(textPage);

            A.CallTo(() => session.Delete(textPage)).MustHaveHappened();
        }

        [Fact]
        public void DocumentService_GetDocumentVersion_CallsSessionGetDocumentVersionWithSpecifiedId()
        {
            var session = A.Fake<ISession>();
            var documentService = new DocumentService(session, new SiteSettings());

            documentService.GetDocumentVersion(1);

            A.CallTo(() => session.Get<DocumentVersion>(1)).MustHaveHappened();
        }

        [Fact]
        public void DocumentService_GetDocumentVersion_ReturnsResultOfCallToSessionGet()
        {
            var session = A.Fake<ISession>();
            var documentVersion = new DocumentVersion();
            A.CallTo(() => session.Get<DocumentVersion>(1)).Returns(documentVersion);
            var documentService = new DocumentService(session, new SiteSettings());

            var version = documentService.GetDocumentVersion(1);
            version.Should().Be(documentVersion);
        }
    }
}