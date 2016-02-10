﻿using System;
using UIKit;
using Foundation;
using CodeHub.iOS.Services;
using System.Threading.Tasks;
using System.Linq;
using CodeHub.Core.Services;
using CodeHub.iOS.ViewControllers;
using BigTed;
using System.Reactive.Disposables;
using CodeHub.iOS.WebViews;
using CodeHub.iOS.Views;
using MvvmCross.Platform;

namespace CodeHub.iOS.ViewControllers.Application
{
    public class UpgradeViewController : WebView
    {
        private readonly IFeaturesService _featuresService;
        private readonly IInAppPurchaseService _inAppPurchaseService;
        private UIActivityIndicatorView _activityView;

        public UpgradeViewController() : base(false, false)
        {
            _featuresService = Mvx.Resolve<IFeaturesService>();
            _inAppPurchaseService = Mvx.Resolve<IInAppPurchaseService>();
            ViewModel = new CodeHub.Core.ViewModels.App.UpgradeViewModel();
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            Title = "Pro Upgrade";

            _activityView = new UIActivityIndicatorView
            {
                Color = Theme.CurrentTheme.PrimaryColor,
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth,
            };
            _activityView.Frame = new CoreGraphics.CGRect(0, 44, View.Frame.Width, 88f);

            Load().ToBackground();
        }

        private async Task Load()
        {
            Web.UserInteractionEnabled = false;
            Web.LoadHtmlString("", NSBundle.MainBundle.BundleUrl);

            _activityView.Alpha = 1;
            _activityView.StartAnimating();
            View.Add(_activityView);

            try
            {
                var productData = (await _inAppPurchaseService.RequestProductData(FeaturesService.ProEdition)).Products.FirstOrDefault();
                var enabled = _featuresService.IsProEnabled;
                var model = new UpgradeDetailsModel(productData != null ? productData.LocalizedPrice() : null, enabled);
                var content = new UpgradeDetailsRazorView { Model = model }.GenerateString();
                LoadContent(content);
                Web.UserInteractionEnabled = true;
            }
            finally
            {
                UIView.Animate(0.2f, 0, UIViewAnimationOptions.BeginFromCurrentState | UIViewAnimationOptions.CurveEaseInOut,
                    () => _activityView.Alpha = 0, () =>
                    {
                        _activityView.RemoveFromSuperview();
                        _activityView.StopAnimating();
                    });
            }
        }

        protected override bool ShouldStartLoad(WebKit.WKWebView webView, WebKit.WKNavigationAction navigationAction)
        {
            var url = navigationAction.Request.Url;

            if (url.Scheme.Equals("app"))
            {
                var func = url.Host;

                if (string.Equals(func, "buy", StringComparison.OrdinalIgnoreCase))
                {
                    Activate(_featuresService.ActivatePro).ToBackground();
                }
                else if (string.Equals(func, "restore", StringComparison.OrdinalIgnoreCase))
                {
                    Activate(_featuresService.RestorePro).ToBackground();
                }

                return false;
            }

            if (url.Scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase))
            {
                UIApplication.SharedApplication.OpenUrl(url);
                return false;
            }

            if (url.Scheme.Equals("file"))
            {
                return true;
            }

            if (url.Scheme.Equals("http") || url.Scheme.Equals("https"))
            {
                var view = new WebBrowserViewController(url.AbsoluteString);
                view.NavigationItem.LeftBarButtonItem = new UIBarButtonItem(Theme.CurrentTheme.CancelButton, UIBarButtonItemStyle.Done, 
                    (s, e) => DismissViewController(true, null));
                PresentViewController(new ThemedNavigationController(view), true, null);
                return false;
            }

            return false;
        }

        private async Task Activate(Func<Task> activation)
        {
            try
            {
                BTProgressHUD.ShowContinuousProgress("Activating...", ProgressHUD.MaskType.Gradient);
                using (Disposable.Create(BTProgressHUD.Dismiss))
                    await activation();
                
                BTProgressHUD.ShowSuccessWithStatus("Activated!");
                Load().ToBackground();
            }
            catch (Exception e)
            {
                MonoTouch.Utilities.ShowAlert("Error", e.Message);
            }
        }
    }
}
