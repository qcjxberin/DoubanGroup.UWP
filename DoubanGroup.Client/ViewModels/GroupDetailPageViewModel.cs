﻿using DoubanGroup.Core.Api.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Windows.Navigation;
using DoubanGroup.Client.Models;
using DoubanGroup.Core.Api;
using Prism.Commands;
using Windows.UI.Xaml.Navigation;
using DoubanGroup.Client.CacheItem;
using NotificationsExtensions.Tiles;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;

namespace DoubanGroup.Client.ViewModels
{
    public class GroupDetailPageViewModel : ViewModelBase
    {
        private long _groupID;

        public long GroupID
        {
            get { return _groupID; }
            set { this.SetProperty(ref _groupID, value); }
        }

        private Group _group;

        public Group Group
        {
            get { return _group; }
            set { this.SetProperty(ref _group, value); }
        }

        public bool IsGroupMember
        {
            get
            {
                return this.CurrentUser.IsGroupMember(this.GroupID);
            }
        }

        public IncrementalLoadingList<Topic> TopicList { get; private set; }

        public IncrementalLoadingList<User> UserList { get; private set; }

        public GroupDetailPageViewModel()
        {
            this.TopicList = new IncrementalLoadingList<Topic>(this.LoadTopics);
            this.UserList = new IncrementalLoadingList<User>(this.LoadUsers);
        }

        public override async void OnNavigatedTo(NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
        {
            base.OnNavigatedTo(e, viewModelState);

            this.GroupID = (long)e.Parameter;

            if (e.NavigationMode == NavigationMode.New)
            {
                this.LoadGroup();
            }
        }

        private async Task<IEnumerable<Topic>> LoadTopics(uint count)
        {
            this.IsLoading = true;

            var topicList = await this.ApiClient.GetGroupTopics(this.GroupID, this.TopicList.Count, 30);

            if (topicList.Items.Count < 30)
            {
                this.TopicList.NoMore();
            }

            this.IsLoading = false;

            return topicList.Items;
        }

        private async Task<IEnumerable<User>> LoadUsers(uint count)
        {
            this.IsLoading = true;

            var userList = await this.ApiClient.GetGroupMembers(this.GroupID, this.UserList.Count, 100);

            if (userList.Items.Count < 100)
            {
                this.UserList.NoMore();
            }

            this.IsLoading = false;

            return userList.Items;
        }

        private async Task LoadGroup()
        {
            var group = await this.ApiClient.GetGroup(this.GroupID);
            this.Group = group;
            this.OnPropertyChanged(() => this.IsGroupMember);
        }

        private DelegateCommand _viewMembersCommand;

        public DelegateCommand ViewMembersCommand
        {
            get
            {
                if (_viewMembersCommand == null)
                {
                    _viewMembersCommand = new DelegateCommand(this.ViewMembers);
                }
                return _viewMembersCommand;
            }
        }

        private void ViewMembers()
        {
            this.NavigationService.Navigate("GroupMembers", this.GroupID);
        }

        private DelegateCommand _joinGroupCommand;

        public DelegateCommand JoinGroupCommand
        {
            get
            {
                if (_joinGroupCommand == null)
                {
                    _joinGroupCommand = new DelegateCommand(this.JoinGroup);
                }
                return _joinGroupCommand;
            }
        }

        private async void JoinGroup()
        {
            if (!await this.RequireLogin())
            {
                return;
            }

            if (this.IsLoading)
            {
                return;
            }

            this.IsLoading = true;

            if (this.Group.JoinType == "A")
            {
                try
                {
                    await this.ApiClient.JoinGroup(this.GroupID);
                    this.EventAggretator.GetEvent<Events.JoinGroupEvent>().Publish(this.Group);
                    this.OnPropertyChanged(() => this.IsGroupMember);
                }
                catch (ApiException ex)
                {
                    this.Alert(ex.Message);
                }
            }
            else
            {

            }

            this.IsLoading = false;
        }

        private DelegateCommand _quitGroupCommand;

        public DelegateCommand QuitGroupCommand
        {
            get
            {
                if (_quitGroupCommand == null)
                {
                    _quitGroupCommand = new DelegateCommand(QuitGroup);
                }
                return _quitGroupCommand;
            }
        }

        private async void QuitGroup()
        {
            if (!await this.RequireLogin())
            {
                return;
            }

            if (this.IsLoading)
            {
                return;
            }

            if (!await this.Confirm("确认退出该小组?"))
            {
                return;
            }

            this.IsLoading = true;

            try
            {
                await this.ApiClient.QuitGroup(this.GroupID);
                this.EventAggretator.GetEvent<Events.QuitGroupEvent>().Publish(this.Group);
                this.OnPropertyChanged(() => this.IsGroupMember);
            }
            catch (ApiException ex)
            {
                this.Alert(ex.Message);
            }

            this.IsLoading = false;
        }

        private DelegateCommand _pinCommand;

        public DelegateCommand PinCommand
        {
            get
            {
                if (_pinCommand == null)
                {
                    _pinCommand = new DelegateCommand(this.Pin);
                }
                return _pinCommand;
            }
        }

        private async void Pin()
        {
            string tileID = $"Tile_Group_{this.GroupID}";
            string argument = $"GroupDetail-{this.GroupID}";

            SecondaryTile tile = new SecondaryTile(tileID, this.Group.Name, argument, new Uri("ms-appx:///Assets/logo-50.png"), TileSize.Square150x150);
            await tile.RequestCreateAsync();

            TileBindingContentAdaptive bindingContent = new TileBindingContentAdaptive()
            {
                PeekImage = new TilePeekImage()
                {
                    Source = new TileImageSource(this.Group.LargeAvatar)
                }
            };

            TileBinding binding = new TileBinding()
            {
                Branding = TileBranding.NameAndLogo,

                DisplayName = this.Group.Name,

                Content = bindingContent
            };

            TileContent content = new TileContent()
            {
                Visual = new TileVisual()
                {
                    TileMedium = binding,
                    TileWide = binding,
                    TileLarge = binding
                }
            };

            var notification = new TileNotification(content.GetXml());

            var updater = TileUpdateManager.CreateTileUpdaterForSecondaryTile(tileID);
            updater.Update(notification);
        }
    }
}
