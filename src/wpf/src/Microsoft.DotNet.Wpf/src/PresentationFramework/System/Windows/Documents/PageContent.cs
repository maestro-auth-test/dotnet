﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using MS.Internal;
using MS.Internal.Documents;
using MS.Internal.Utility;
using System.Windows.Navigation;
using System.Windows.Markup;
using System.Windows.Threading;               // Dispatcher
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;

using MS.Utility;

//
// Description:
//      Implements the PageContent element
//

namespace System.Windows.Documents
{
    //=====================================================================
    /// <summary>
    /// PageContent is the class that references or directly hosts a page stream.
    /// Each page stream represents the content of a single page.  Each has its
    /// own isolated tree, ID space, property space, and resource table.
    ///
    /// The main function of PageContent is to load/parse the page content it
    /// references, and produce Visual tree that represents it page visual. A
    /// page visual tree always rooted at FixedPage.
    /// </summary>
    [ContentProperty("Child")]
    public sealed class PageContent : FrameworkElement, IAddChildInternal, IUriContext
    {
        //--------------------------------------------------------------------
        //
        // Connstructors
        //
        //---------------------------------------------------------------------

        #region Constructors
        /// <summary>
        ///     Default PageContent constructor
        /// </summary>
        /// <remarks>
        ///     Automatic determination of current Dispatcher. Use alternative constructor
        ///     that accepts a Dispatcher for best performance.
        /// </remarks>
        public PageContent() : base()
        {
            _Init();
        }
        #endregion Constructors

        //--------------------------------------------------------------------
        //
        // Public Methods
        //
        //---------------------------------------------------------------------

        #region Public Methods
        /// <summary>
        /// Synchonrously load and parse the page's tree
        /// </summary>
        /// <param name="forceReload">Force reloading the page tree instead of using cached value</param>
        /// <returns>The root FixedPage of the page's tree</returns>
        public FixedPage GetPageRoot(bool forceReload)
        {
#if DEBUG
            DocumentsTrace.FixedFormat.PageContent.Trace($"PageContent.GetPageRoot Source={(Source ?? new Uri("", UriKind.RelativeOrAbsolute))}");
#endif

//             VerifyAccess();
            _asyncOp?.Wait();

            FixedPage p = null;

            if (!forceReload)
            {
                // If page was previously loaded, return the loaded page.
                p = _GetLoadedPage();
            }

            if (p == null)
            {
                p = _LoadPage();
            }

            return p;
        }

        /// <summary>
        /// Initiate an asynchoronus request to load the page tree.
        /// </summary>
        /// <param name="forceReload">Force reloading the page tree instead of using cached value</param>
        public void GetPageRootAsync(bool forceReload)
        {
#if DEBUG
            DocumentsTrace.FixedFormat.PageContent.Trace($"PageContent.GetPageRootAsync Source={(Source ?? new Uri("", UriKind.RelativeOrAbsolute))}");
#endif

//             VerifyAccess();

            if (_asyncOp != null)
            {
                return;
            }

            FixedPage p = null;
            if (!forceReload)
            {
                p = _GetLoadedPage();
            }


            if (p != null)
            {
                _NotifyPageCompleted(p, null, false, null);
            }
            else
            {
                Dispatcher dispatcher = this.Dispatcher;
                Uri uriToLoad = _ResolveUri();

                if (uriToLoad != null || _child != null)
                {
                    _asyncOp = new PageContentAsyncResult(new AsyncCallback(_RequestPageCallback), null, dispatcher, uriToLoad, uriToLoad, _child);
                    _asyncOp.DispatcherOperation = dispatcher.BeginInvoke(DispatcherPriority.Normal, new DispatcherOperationCallback(_asyncOp.Dispatch), null);
                }
            }
        }

        /// <summary>
        /// Cancel outstanding async call
        /// </summary>
        public void GetPageRootAsyncCancel()
        {
#if DEBUG
            DocumentsTrace.FixedFormat.PageContent.Trace($"PageContent.GetPageRootAsyncCancel Source={(Source ?? new Uri("", UriKind.RelativeOrAbsolute))}");
#endif
//             VerifyAccess();
            // Important: do not throw if no outstanding GetPageRootAsyncCall
            _asyncOp?.Cancel();
            _asyncOp = null;
        }
        #endregion Public Methods

        #region IAddChild
        ///<summary>
        /// Called to Add the object as a Child.
        ///</summary>
        /// <exception cref="ArgumentNullException">value is NULL.</exception>
        /// <exception cref="ArgumentException">value is not of type FixedPage.</exception>
        /// <exception cref="InvalidOperationException">A child already exists (a PageContent can have at most one child).</exception>
        ///<param name="value">
        /// Object to add as a child
        ///</param>
        /// <ExternalAPI/>
        void IAddChild.AddChild(Object value)
        {
//             VerifyAccess();

            ArgumentNullException.ThrowIfNull(value);


            FixedPage fp = value as FixedPage;
            if (fp == null)
            {
                throw new ArgumentException(SR.Format(SR.UnexpectedParameterType, value.GetType(), typeof(FixedPage)), nameof(value));
            }

            if (_child != null)
            {
                throw new InvalidOperationException(SR.Format(SR.CanOnlyHaveOneChild, typeof(PageContent), value));
            }

            _pageRef = null;
            _child   = fp;
            LogicalTreeHelper.AddLogicalChild(this, _child);
        }

        ///<summary>
        /// Called when text appears under the tag in markup
        ///</summary>
        ///<param name="text">
        /// Text to Add to the Object
        ///</param>
        /// <ExternalAPI/>
        void IAddChild.AddText(string text)
        {
            XamlSerializerUtil.ThrowIfNonWhiteSpaceInAddText(text, this);
        }
        #endregion


        //--------------------------------------------------------------------
        //
        // Public Properties
        //
        //---------------------------------------------------------------------

        #region Public Properties
        /// <summary>
        /// Dynamic Property to reference an external page stream.
        /// </summary>
        public static readonly DependencyProperty SourceProperty =
                DependencyProperty.Register(
                        "Source",
                        typeof(Uri),
                        typeof(PageContent),
                        new FrameworkPropertyMetadata(
                                (Uri) null,
                                new PropertyChangedCallback(OnSourceChanged)));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PageContent p = (PageContent) d;
            p._pageRef = null;
        }

        /// <summary>
        /// Get/Set Source property that references an external page stream.
        /// </summary>
        public Uri Source
        {
            get { return (Uri) GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        /// <summary>
        /// Return LinkTargets. Note it is read only and return non-null collection to satify parser's requirement.
        /// </summary>
        public LinkTargetCollection LinkTargets
        {
            get
            {
                if (_linkTargets == null)
                {
                    _linkTargets = new LinkTargetCollection();
                }
                return _linkTargets;
            }
        }

        /// <summary>
        /// The fixed page content. This property will be available in the following conditions:
        /// 1) PageContent has an immediate FixedPage child in the markup
        /// 2) FixedPage has been set as a Child of this object using IAddChild
        /// On the other hand, if PageContent has a Source property for loading a FixedPage,
        /// the FixedPage will not be cached even it has been previously loaded
        /// In this case, the correct API method to use would be GetPageRoot or GetPageRootAsync
        /// </summary>
        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public FixedPage Child
        {
            get
            {
                return _child;
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                if (_child != null)
                {
                    throw new InvalidOperationException(SR.Format(SR.CanOnlyHaveOneChild, typeof(PageContent), value));
                }

                _pageRef = null;
                _child = value;
                LogicalTreeHelper.AddLogicalChild(this, _child);
            }
        }

        /// <summary>
        /// Prevent writing the property when using XamlPageContentSerializer
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ShouldSerializeChild(XamlDesignerSerializationManager manager)
        {
            bool shouldSerialize = false;
            if (manager != null)
            {
                shouldSerialize = (manager.XmlWriter == null);
            }
            return shouldSerialize;
        }

        #endregion Public Properties

        #region IUriContext
        /// <summary>
        /// <see cref="IUriContext.BaseUri" />
        /// </summary>
        Uri IUriContext.BaseUri
        {
            get
            {
                return (Uri)GetValue(BaseUriHelper.BaseUriProperty);
            }
            set
            {
                SetValue(BaseUriHelper.BaseUriProperty, value);
            }
        }
        #endregion IUriContext

        //--------------------------------------------------------------------
        //
        // Public Events
        //
        //---------------------------------------------------------------------

        #region Public Event
        /// <summary>
        /// Event to notify GetPageRootAsync completed
        /// </summary>
        public event GetPageRootCompletedEventHandler GetPageRootCompleted;
        #endregion Public Event


        //--------------------------------------------------------------------
        //
        // Internal Methods
        //
        //---------------------------------------------------------------------


        #region Internal Methods
        /// <summary>
        /// Check to see if a page visual is created by this PageContent.
        /// </summary>
        /// <param name="pageVisual"></param>
        /// <returns></returns>
        internal bool IsOwnerOf(FixedPage pageVisual)
        {
//             VerifyAccess();
            ArgumentNullException.ThrowIfNull(pageVisual);

            if (_child == pageVisual)
            {
                return true;
            }

            if (_pageRef != null)
            {
                FixedPage c = (FixedPage)_pageRef.Target;
                if (c == pageVisual)
                {
                    return true;
                }
            }
            return false;
        }

        internal Stream GetPageStream()
        {
            Uri uriToLoad = _ResolveUri();
            Stream pageStream = null;

            if (uriToLoad != null)
            {
                pageStream = WpfWebRequestHelper.CreateRequestAndGetResponseStream(uriToLoad);
                if (pageStream == null)
                {
                    throw new ApplicationException(SR.PageContentNotFound);
                }
            }

            return pageStream;
        }
        #endregion Internal Methods


        //--------------------------------------------------------------------
        //
        // Internal Properties
        //
        //---------------------------------------------------------------------

        #region Internal Properties
        // return nested PageStream
        internal FixedPage PageStream
        {
            get
            {
                return _child;
            }
        }

        /// <summary>
        /// Search for the _LinkTargetCollection for named element in this fixedPage
        /// </summary>
        /// <param name="elementID"></param>
        /// <returns></returns>
        internal bool ContainsID(string elementID)
        {
            bool boolRet = false;
            foreach (LinkTarget item in LinkTargets)
            {
                if (elementID.Equals(item.Name))
                {
                    boolRet = true;
                    break;
                }
            }

            return boolRet;
        }
        /// <summary>
        /// Puts FixedPage in the logical tree if it is already loaded.
        /// </summary>
        protected internal override System.Collections.IEnumerator LogicalChildren
        {
            get
            {
                FixedPage[] children;
                FixedPage child = _child;
                if (child == null)
                {
                    child = _GetLoadedPage();
                }

                // Should this trigger a load?  We are not doing so, but if the
                // page has been loaded it has the page content as its logical
                // parent, so this must be consistent.

                if (child == null)
                {
                    children = Array.Empty<FixedPage>();
                }
                else
                {
                    children = new FixedPage[] { child };
                }

                return children.GetEnumerator();
            }
        }

        #endregion Internal Properties

        //--------------------------------------------------------------------
        //
        // private Properties
        //
        //---------------------------------------------------------------------

        #region Private Properties
        #endregion Private Properties


        //--------------------------------------------------------------------
        //
        // Private Methods
        //
        //---------------------------------------------------------------------

        #region Private Methods
        private void  _Init()
        {
            InheritanceBehavior = InheritanceBehavior.SkipToAppNow;
            _pendingStreams = new HybridDictionary();
        }

        private void _NotifyPageCompleted(FixedPage result, Exception error, bool cancelled, object userToken)
        {
            if (GetPageRootCompleted != null)
            {
                GetPageRootCompletedEventArgs e = new GetPageRootCompletedEventArgs(result, error, cancelled, userToken);
                GetPageRootCompleted(this, e);
            }
        }

        private Uri _ResolveUri()
        {
            Uri uriToNavigate = this.Source;

            if (uriToNavigate != null)
            {
                uriToNavigate = BindUriHelper.GetUriToNavigate(this, ((IUriContext)this).BaseUri, uriToNavigate);
            }
            return uriToNavigate;
        }

        private void _RequestPageCallback(IAsyncResult ar)
        {
            PageContentAsyncResult par = (PageContentAsyncResult)ar;
            if (par == _asyncOp && par.Result != null)
            {
                //par.Result.IsTreeSeparator = true;
                LogicalTreeHelper.AddLogicalChild(this, par.Result);
                _pageRef = new WeakReference(par.Result);
            }

            // Set outstanding async op to null to allow for reentrancy during callback.
            _asyncOp = null;
            _NotifyPageCompleted(par.Result, par.Exception, par.IsCancelled, par.AsyncState);
        }


        // sync load a page
        private FixedPage _LoadPage()
        {
            EventTrace.EasyTraceEvent(EventTrace.Keyword.KeywordXPS, EventTrace.Event.WClientDRXGetPageBegin);

            FixedPage p = null;
            Stream pageStream;
            try
            {
                if (_child != null)
                {
                    p = _child;
                }
                else
                {
                    Uri uriToLoad = _ResolveUri();

                    if (uriToLoad != null)
                    {
                        _LoadPageImpl(((IUriContext)this).BaseUri, uriToLoad, out p, out pageStream);

                        if (p == null || p.IsInitialized)
                        {
                            pageStream.Close();
                        }
                        else
                        {
                            _pendingStreams.Add(p, pageStream);
                            p.Initialized += new EventHandler(_OnPaserFinished);
                        }
                    }
                }

                if (p != null)
                {
                    LogicalTreeHelper.AddLogicalChild(this, p);
                    _pageRef = new WeakReference(p);
                }
                else
                {
                    _pageRef = null;
                }
            }
            finally
            {
                EventTrace.EasyTraceEvent(EventTrace.Keyword.KeywordXPS, EventTrace.Event.WClientDRXGetPageEnd);
            }

            return p;
        }

        private FixedPage _GetLoadedPage()
        {
            FixedPage p = null;
            if (_pageRef != null)
            {
                p = (FixedPage)_pageRef.Target;
            }

            return p;
        }

        private void _OnPaserFinished(object sender, EventArgs args)
        {
            if (_pendingStreams.Contains(sender))
            {
                Stream pageStream = (Stream)_pendingStreams[sender];
                pageStream.Close();
                _pendingStreams.Remove(sender);
            }
        }

        internal static void _LoadPageImpl(Uri baseUri, Uri uriToLoad, out FixedPage fixedPage, out Stream pageStream)
        {
            ContentType mimeType;
            pageStream = WpfWebRequestHelper.CreateRequestAndGetResponseStream(uriToLoad, out mimeType);
            object o = null;
            if (pageStream == null)
            {
                throw new ApplicationException(SR.PageContentNotFound);
            }

            ParserContext pc = new ParserContext
            {
                BaseUri = uriToLoad
            };

            if (BindUriHelper.IsXamlMimeType(mimeType))
            {
                XpsValidatingLoader loader = new XpsValidatingLoader();
                o = loader.Load(pageStream, baseUri, pc, mimeType);
            }
            else if (MS.Internal.MimeTypeMapper.BamlMime.AreTypeAndSubTypeEqual(mimeType))
            {
                o = XamlReader.LoadBaml(pageStream, pc, null, true);
            }
            else
            {
                throw new ApplicationException(SR.PageContentUnsupportedMimeType);
            }

            if (o != null && !(o is FixedPage))
            {
                throw new ApplicationException(SR.Format(SR.PageContentUnsupportedPageType, o.GetType()));
            }

            fixedPage =  (FixedPage)o;
        }

        #endregion Private Methods

        //--------------------------------------------------------------------
        //
        // Private Fields
        //
        //---------------------------------------------------------------------

        #region Private Fields
        private WeakReference _pageRef;         // weak ref to page's root visual
        private FixedPage     _child;              // directly hosted page stream
        private PageContentAsyncResult  _asyncOp;
        private HybridDictionary  _pendingStreams;
        private LinkTargetCollection _linkTargets;
        #endregion Private Fields
    }


    /// <summary>
    /// EventArgs to return result from GetPageRootAsync
    /// </summary>
    public sealed class GetPageRootCompletedEventArgs : AsyncCompletedEventArgs
    {
        internal GetPageRootCompletedEventArgs(FixedPage page, Exception error, bool cancelled, object userToken)
            : base(error, cancelled, userToken)
        {
            _page = page;
        }

        /// <summary>
        /// The page tree resulted from GetPageRootAsync
        /// </summary>
        public FixedPage Result
        {
            get
            {
                this.RaiseExceptionIfNecessary();
                return _page;
            }
        }

        private FixedPage _page;
    }

    /// <summary>
    /// Delegate for GetPageRootAsync compeleted notification
    /// </summary>
    /// <param name="sender">The PageContent object that fired this notification</param>
    /// <param name="e">Event Arguments</param>
    public delegate void GetPageRootCompletedEventHandler(object sender, GetPageRootCompletedEventArgs e);
}

