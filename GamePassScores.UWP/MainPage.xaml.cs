﻿using GamePassScores.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Windows.Storage;
using System.Collections.ObjectModel;
using MUXC = Microsoft.UI.Xaml.Controls;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.UI.Xaml.Media.Animation;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Windows.System.Profile;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace GamePassScores.UWP
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            CacheFolderChecked += App_CacheFolderChecked;
            CheckCacheFolder();
            //从json文件读取游戏信息

        }

        private async void CheckCacheFolder()
        {
            var applicationFolder = ApplicationData.Current.LocalFolder;
            App.CacheFolder = (await ApplicationData.Current.LocalFolder.TryGetItemAsync("PostersCache")) as StorageFolder;
            if (App.CacheFolder == null)
            {
                App.CacheFolder = await applicationFolder.CreateFolderAsync("PostersCache");
            }

            CacheFolderChecked?.Invoke(null, null);
        }

        private event EventHandler CacheFolderChecked;
        private void App_CacheFolderChecked(object sender, EventArgs e)
        {
            ReadGamesFromJson();
        }

        public double GamesViewItemHeight
        {
            get
            {
                return GameViewModel.ItemHeight;
            }
        }
        private async void UpateJsonData()
        {
            var httpClient = new Windows.Web.Http.HttpClient();

            try
            {
                var buffer = await httpClient.GetBufferAsync(new Uri("https://github.com/XeonKHJ/GamePassScores/blob/master/QueryResults/games.json?raw=true"));

                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("games.json", CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteBufferAsync(file, buffer);

                ReadGamesFromJson();

                System.Diagnostics.Debug.WriteLine("更新成功");
            }
            catch(Exception exception)
            {
                System.Diagnostics.Debug.WriteLine("网络不好，无法更新。");
                _isRefreshing = false;
            }
        }
        private async void ReadGamesFromJson()
        {
            //先检查有没有下载到games.json
            var downloadedJsonFile = new FileInfo(ApplicationData.Current.LocalFolder.Path + "\\games.json");

            StorageFile jsonFile = null;
            if (downloadedJsonFile.Exists)
            {
                jsonFile = await StorageFile.GetFileFromPathAsync(ApplicationData.Current.LocalFolder.Path + "\\games.json");
            }
            else
            {
                jsonFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/games.json"));

                //在后台下载Gamepass游戏数据
                UpateJsonData();
            }


            var jsonString = await FileIO.ReadTextAsync(jsonFile);
            var games = JsonConvert.DeserializeObject<List<Game>>(jsonString);
            HashSet<string> genre = new HashSet<string>();

            
            foreach (var game in games)
            {
                foreach (var c in game.Categories)
                {
                    genre.Add(c);
                }

                GamesViewModel.Add(new GameViewModel(game));
                if (game.MetaScore.Count == 0)
                {
                    game.MetaScore.Add(Platform.Unknown, -1);
                }

            }
            List<string> orderedGenre = genre.OrderBy(g => g).ToList();
            Categories.Clear();
            foreach (var g in orderedGenre)
            {
                Categories.Add(new CategorieViewModel(g));
            }
            Games = games;

            //SearchBox_TextChanged(SearchBox, null);
            OrderByNameAscendItem_Click(null, null);

            _isRefreshing = false;
        }

        public List<Game> Games = new List<Game>();
        public ObservableCollection<GameViewModel> GamesViewModel { get; set; } = new ObservableCollection<GameViewModel>();

        public ObservableCollection<CategorieViewModel> Categories { set; get; } = new ObservableCollection<CategorieViewModel>();
        private void OrderByScoreAscendItem_Click(object sender, RoutedEventArgs e)
        {
            Games = Games.OrderBy(g => g.MetaScore.First().Value).ToList();
            //SearchBox_TextChanged
            //GamesViewModel.Clear();
            //foreach(var g in Games)
            //{
            //    GamesViewModel.Add(new GameViewModel(g));
            //}
            SearchBox_TextChanged(SearchBox, null);
        }

        private void OrderByScoreDescendItem_Click(object sender, RoutedEventArgs e)
        {
            Games = Games.OrderByDescending(g => g.MetaScore.First().Value).ToList();

            //GamesViewModel.Clear();
            //foreach (var g in Games)
            //{
            //    GamesViewModel.Add(new GameViewModel(g));
            //}
            SearchBox_TextChanged(SearchBox, null);
        }

        UIElement animatingElement;
        private void GamesView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var container = GamesView.ContainerFromItem(e.ClickedItem) as GridViewItem;
            if (container != null)
            {
                //find the image
                var root = (FrameworkElement)container.ContentTemplateRoot;
                var image = (UIElement)root.FindName("PosterImage");
                animatingElement = image;
                //prepare the animation
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ForwardConnectedAnimation", image);
            }

            //ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ForwardConnectedAnimation", sender);
            Frame.Navigate(typeof(GameDetailPage), e.ClickedItem);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            switch (AnalyticsInfo.VersionInfo.DeviceFamily)
            {
                case "Windows.Xbox":
                    GamesView.DesiredWidth = 160;
                    GamesView.ItemHeight = 240;
                    break;
            }


            if (animatingElement != null)
            {
                var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("BackwardConnectedAnimation");
                if (anim != null)
                {
                    anim.TryStart(animatingElement);
                }
                animatingElement = null;
            }
        }

        private bool _isAboutDialogueOpened = false;
        private async void AboutButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            //switch (AnalyticsInfo.VersionInfo.DeviceFamily)
            //{
            //    case "Windows.Desktop":
            //        AboutTip.IsOpen = true;
            //        break;
            //}
            AboutDialogue aboutDialogue = new AboutDialogue();
            _isAboutDialogueOpened = true;
            var result = await aboutDialogue.ShowAsync();
            _isAboutDialogueOpened = false;
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            //SearchBox.TextChanged += SearchBox_TextChanged;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox searchBlock = sender as TextBox;
            string text = searchBlock.Text;
            var gamesFilteredByCategories = FilterByCategorie(Games.ToArray());
            if(gamesFilteredByCategories == null)
            {
                gamesFilteredByCategories = Games.ToArray();
            }
            Game[] gamesFilteredByTiming = FilterByTiming(gamesFilteredByCategories);
            if (text.Trim() != string.Empty || gamesFilteredByTiming != null)
            {
                if (gamesFilteredByTiming == null)
                {
                    gamesFilteredByTiming = Games.ToArray();
                }
                var games = (from g in gamesFilteredByTiming
                             where g.Title.First().Value.ToLower().Contains(text.ToLower().Trim())
                             select g).ToArray();


                bool isViewModelChanged = false;
                if (games.Length == GamesViewModel.Count)
                {
                    for (int i = 0; i < games.Length; ++i)
                    {
                        if (games[i].ID != GamesViewModel[i].ID)
                        {
                            isViewModelChanged = true;
                            break;
                        }
                    }
                }
                else
                {
                    isViewModelChanged = true;
                }

                if (isViewModelChanged)
                {
                    GamesViewModel.Clear();
                    foreach (var g in games)
                    {
                        GamesViewModel.Add(new GameViewModel(g));
                    }
                }

            }
            else
            {
                GamesViewModel.Clear();
                foreach (var game in Games)
                {
                    GamesViewModel.Add(new GameViewModel(game));
                }
            }
        }

        private Game[] FilterByTiming(Game[] gamesFilteredByCategories)
        {
            //查看选项
            var buttons = InVaultTimeRadioButtons;

            Game[] filteredGames = gamesFilteredByCategories;

            if(RcentlyAddedRadioButton!= null && (bool)RcentlyAddedRadioButton.IsChecked)
            {
                filteredGames = (from g in gamesFilteredByCategories where g.InVaultTime == InVaultTime.RecentlyAdded select g).ToArray();
            }
            else if(LeavingSoonRadioButton != null && (bool)LeavingSoonRadioButton.IsChecked)
            {
                filteredGames = (from g in gamesFilteredByCategories where g.InVaultTime == InVaultTime.LeavingSoon select g).ToArray();
            }

            return filteredGames;
        }

        private void PosterImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            Image image = sender as Image;

            if (image != null && image.Tag != null)
            {
                var match = (from g in GamesViewModel where g.ID == image.Tag.ToString() select g).ToList();
                if (match.Count != 0)
                {
                    match.First().IsImageLoaded = Visibility.Collapsed;
                }
            }
        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void OrderByNameAscendItem_Click(object sender, RoutedEventArgs e)
        {
            Games = Games.OrderBy(g => g.Title.First().Value).ToList();

            //GamesViewModel.Clear();
            //foreach (var g in Games)
            //{
            //    GamesViewModel.Add(new GameViewModel(g));
            //}
            SearchBox_TextChanged(SearchBox, null);
        }

        private void OrderByNameDescendItem_Click(object sender, RoutedEventArgs e)
        {
            Games = Games.OrderByDescending(g => g.Title.First().Value).ToList();

            //GamesViewModel.Clear();
            //foreach (var g in Games)
            //{
            //    GamesViewModel.Add(new GameViewModel(g));
            //}
            SearchBox_TextChanged(SearchBox, null);
        }

        /// <summary>
        /// 通过类别筛选
        /// </summary>
        /// <returns>若为null就是不用筛选，如果要筛选且一个都没有会返回一个空数组的！</returns>
        private Game[] FilterByCategorie(Game[] games)
        {
            Game[] selectedGames = null;

            HashSet<string> checkedcategories = new HashSet<string>();
            foreach (var c in Categories)
            {
                if (c.IsChecked == true)
                {
                    checkedcategories.Add(c.Categorie);
                }
            }

            if (checkedcategories.Count != 0)
            {
                selectedGames = (from g in games
                                 where g.Categories.Intersect(checkedcategories).Count() != 0
                                 select g).ToArray();
            }

            return selectedGames;
        }
        private void CategorieCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SearchBox_TextChanged(SearchBox, null);
        }

        private void CategorieCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SearchBox_TextChanged(SearchBox, null);
        }

        private bool _isRefreshing = false;
        private float _angle = 360;
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _isRefreshing = true;
            UpateJsonData();
            while (_isRefreshing)
            {
                await RefreshButtonIcon.Rotate(value: _angle, centerX: 10.0f, centerY: 10.0f, duration: 1000, delay: 0, easingType: EasingType.Default).StartAsync();
                _angle += 360;
            }
        }

        //private void InVaultTimeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    SearchBox_TextChanged(SearchBox, null);
        //}

        private void TimingRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            SearchBox_TextChanged(SearchBox, null);
        }

        private void InVaultTimeRadioButtons_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("InVaultTimeRadioButtons_GotFocus");
        }

        private void InVaultTimeRadioButtons_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as RadioButton;

            if(item != null)
            {
                item.IsChecked = true;
            }
            
        }

        private void CategoriesView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as CategorieViewModel;

            if (item != null)
            {
                item.IsChecked = !(item.IsChecked);
            }
        }

        private void StackPanel_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("StackPanel_GotFocus");

            if(!OrderBar.IsOpen)
            {
                OrderBar.Focus(FocusState.Programmatic);
                OrderBar.IsOpen = true;
            }
        }

        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(e.Key);
            switch(e.Key)
            {
                case Windows.System.VirtualKey.GamepadMenu:
                    if (!OrderBar.IsOpen)
                    {
                        OrderBar.Focus(FocusState.Programmatic);
                        OrderBar.IsOpen = true;
                    }
                    else
                    {
                        OrderBar.IsOpen = false;
                    }
                    break;
                case Windows.System.VirtualKey.GamepadView:
                    if(!_isAboutDialogueOpened)
                    {
                        AboutButton_ClickAsync(null, null);
                    }
                    
                    break;
            }
        }
    }
}
