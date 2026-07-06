using IronworksTranslator.Models.Settings;
using IronworksTranslator.Utils;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.ViewModels.Pages;
using IronworksTranslator.ViewModels.Windows;
using IronworksTranslator.Views.Windows;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace IronworksTranslator.Views.Pages
{
#pragma warning disable CS8602
    public partial class SettingsPage : INavigableView<SettingsViewModel>
    {
        public SettingsViewModel ViewModel { get; }
        private bool _isInitialized;
        private bool _isChangingTranslatorSelection;
        private bool _isChangingDevicePrioritySelection;
        private MiLMMTModelSize _lastAvailableMiLMMTModelSize;
        private MiLMMTQuantization _lastAvailableMiLMMTQuantization;
        private LocalModelDevicePriority _previousLocalModelDevicePriority;
        private SystemResourceSnapshot? _lastSystemResourceSnapshot;
        private readonly DispatcherTimer resourceTimer;

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            _isInitialized = true;
            ViewModel.CheckPartyFieldIntegrity();
            ViewModel.CheckCommunityIntegrity();
            ViewModel.CheckLinkShellIntegrity();
            ViewModel.CheckCwLinkShellIntegrity();
            ViewModel.CheckSystemIntegrity();
            _previousLocalModelDevicePriority = ViewModel.LocalModelDevicePriority;
            InitializeLastAvailableMiLMMTProfile();
            UpdateTranslatorTooltip(ViewModel.TranslatorEngine);
            EnsureLocalTranslatorModelReady(ViewModel.TranslatorEngine, cbTranslator);

            resourceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2),
            };
            resourceTimer.Tick += (_, _) => UpdateResourceSnapshot();
            resourceTimer.Start();
            UpdateResourceSnapshot();
        }

        private void ChatFontSize_ValueChanged(object sender, RoutedEventArgs e)
        {
            var numberBox = sender as NumberBox;
            numberBox.Value ??= IronworksSettings.Instance.ChatUiSettings.ChatboxFontSize;

            // Immediately update the settings to prevent race condition with new messages
            IronworksSettings.Instance.ChatUiSettings.ChatboxFontSize = (int)numberBox.Value;

            var chatWindowViewModel = App.GetService<ChatWindowViewModel>();
            chatWindowViewModel.ChangeChatFontSize((int)numberBox.Value);
        }

        private void nbDialogueFontSize_ValueChanged(object sender, RoutedEventArgs e)
        {
            var numberBox = sender as NumberBox;
            numberBox.Value ??= IronworksSettings.Instance.ChatUiSettings.DialogueFontSize;

            // Immediately update the settings to prevent race condition with new messages
            IronworksSettings.Instance.ChatUiSettings.DialogueFontSize = (int)numberBox.Value;

            var dialogueWindow = App.GetService<DialogueWindow>();
            dialogueWindow.ChangeDialogueFontSize((int)numberBox.Value);
            //dialogueWindowViewModel.ChangeChatFontSize((int)numberBox.Value);
        }

        private void ChatMargin_ValueChanged(object sender, RoutedEventArgs e)
        {
            var numberBox = sender as NumberBox;
            numberBox.Value ??= IronworksSettings.Instance.ChatUiSettings.ChatMargin;

            // Immediately update the settings to prevent race condition with new messages
            IronworksSettings.Instance.ChatUiSettings.ChatMargin = (int)numberBox.Value;

            var chatWindowViewModel = App.GetService<ChatWindowViewModel>();
            chatWindowViewModel.ChangeChatMargin((int)numberBox.Value);
        }

        private void ChatFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox.SelectedItem == null)
            {
                comboBox.SelectedItem = IronworksSettings.Instance.ChatUiSettings.Font;
            }

            // Immediately update the settings to prevent race condition with new messages
            IronworksSettings.Instance.ChatUiSettings.Font = (string)comboBox.SelectedValue;

            var chatWindowViewModel = App.GetService<ChatWindowViewModel>();
            chatWindowViewModel.ChangeChatFontFamily((string)comboBox.SelectedValue);
        }

        private void GroupPartyFieldComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var comboBox = sender as ComboBox;
            var index = comboBox.SelectedIndex;
            cbEcho.SelectedIndex = index;
            cbSay.SelectedIndex = index;
            cbYell.SelectedIndex = index;
            cbShout.SelectedIndex = index;
            cbTell.SelectedIndex = index;
            cbParty.SelectedIndex = index;
            cbAlliance.SelectedIndex = index;
            cbEmote.SelectedIndex = index;
            cbEmoteCustom.SelectedIndex = index;
        }

        private void GroupLinkShellComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var comboBox = sender as ComboBox;
            var index = comboBox.SelectedIndex;

            cbLinkshell1.SelectedIndex = index;
            cbLinkshell2.SelectedIndex = index;
            cbLinkshell3.SelectedIndex = index;
            cbLinkshell4.SelectedIndex = index;
            cbLinkshell5.SelectedIndex = index;
            cbLinkshell6.SelectedIndex = index;
            cbLinkshell7.SelectedIndex = index;
            cbLinkshell8.SelectedIndex = index;
        }

        private void GroupCommunityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var comboBox = sender as ComboBox;
            var index = comboBox.SelectedIndex;

            cbFreecompany.SelectedIndex = index;
            cbNovice.SelectedIndex = index;
        }

        private void GroupCwLinkShellComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var comboBox = sender as ComboBox;
            var index = comboBox.SelectedIndex;

            cbCwLinkshell1.SelectedIndex = index;
            cbCwLinkshell2.SelectedIndex = index;
            cbCwLinkshell3.SelectedIndex = index;
            cbCwLinkshell4.SelectedIndex = index;
            cbCwLinkshell5.SelectedIndex = index;
            cbCwLinkshell6.SelectedIndex = index;
            cbCwLinkshell7.SelectedIndex = index;
            cbCwLinkshell8.SelectedIndex = index;
        }

        private void GroupSystemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var comboBox = sender as ComboBox;
            var index = comboBox.SelectedIndex;

            cbGameNotice.SelectedIndex = index;
            cbGameSystem.SelectedIndex = index;
            cbGameError.SelectedIndex = index;
            cbNpcDialog.SelectedIndex = index;
            cbNpcAnnounce.SelectedIndex = index;
            cbBossQuotes.SelectedIndex = index;
            cbRecruitment.SelectedIndex = index;
            cbGather.SelectedIndex = index;
            cbMarketSold.SelectedIndex = index;
            cbGilReceive.SelectedIndex = index;
        }

        private void GroupPartyFieldOnButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowEcho.IsChecked = true;
            tsShowSay.IsChecked = true;
            tsShowYell.IsChecked = true;
            tsShowShout.IsChecked = true;
            tsShowTell.IsChecked = true;
            tsShowParty.IsChecked = true;
            tsShowAlliance.IsChecked = true;
            tsShowEmote.IsChecked = true;
            tsShowEmoteCustom.IsChecked = true;
        }

        private void GroupPartyFieldOffButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowEcho.IsChecked = false;
            tsShowSay.IsChecked = false;
            tsShowYell.IsChecked = false;
            tsShowShout.IsChecked = false;
            tsShowTell.IsChecked = false;
            tsShowParty.IsChecked = false;
            tsShowAlliance.IsChecked = false;
            tsShowEmote.IsChecked = false;
            tsShowEmoteCustom.IsChecked = false;
        }

        private void GroupCommunityOnButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowNovice.IsChecked = true;
            tsShowFreecompany.IsChecked = true;
        }

        private void GroupCommunityOffButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowNovice.IsChecked = false;
            tsShowFreecompany.IsChecked = false;
        }

        private void GroupLinkshellOnButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowLinkshell1.IsChecked = true;
            tsShowLinkshell2.IsChecked = true;
            tsShowLinkshell3.IsChecked = true;
            tsShowLinkshell4.IsChecked = true;
            tsShowLinkshell5.IsChecked = true;
            tsShowLinkshell6.IsChecked = true;
            tsShowLinkshell7.IsChecked = true;
            tsShowLinkshell8.IsChecked = true;
        }

        private void GroupLinkshellOffButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowLinkshell1.IsChecked = false;
            tsShowLinkshell2.IsChecked = false;
            tsShowLinkshell3.IsChecked = false;
            tsShowLinkshell4.IsChecked = false;
            tsShowLinkshell5.IsChecked = false;
            tsShowLinkshell6.IsChecked = false;
            tsShowLinkshell7.IsChecked = false;
            tsShowLinkshell8.IsChecked = false;
        }

        private void GroupCWLinkshellOnButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowCwLinkshell1.IsChecked = true;
            tsShowCwLinkshell2.IsChecked = true;
            tsShowCwLinkshell3.IsChecked = true;
            tsShowCwLinkshell4.IsChecked = true;
            tsShowCwLinkshell5.IsChecked = true;
            tsShowCwLinkshell6.IsChecked = true;
            tsShowCwLinkshell7.IsChecked = true;
            tsShowCwLinkshell8.IsChecked = true;
        }

        private void GroupCWLinkshellOffButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowCwLinkshell1.IsChecked = false;
            tsShowCwLinkshell2.IsChecked = false;
            tsShowCwLinkshell3.IsChecked = false;
            tsShowCwLinkshell4.IsChecked = false;
            tsShowCwLinkshell5.IsChecked = false;
            tsShowCwLinkshell6.IsChecked = false;
            tsShowCwLinkshell7.IsChecked = false;
            tsShowCwLinkshell8.IsChecked = false;
        }

        private void GroupSystemOnButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowGameNotice.IsChecked = true;
            tsShowGameSystem.IsChecked = true;
            tsShowGameError.IsChecked = true;
            tsShowNpcDialog.IsChecked = true;
            tsShowNpcAnnounce.IsChecked = true;
            tsShowBossQuotes.IsChecked = true;
            tsShowRecruitment.IsChecked = true;
            tsShowGather.IsChecked = true;
            tsShowMarketSold.IsChecked = true;
            tsShowGilReceive.IsChecked = true;
        }

        private void GroupSystemOffButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowGameNotice.IsChecked = false;
            tsShowGameSystem.IsChecked = false;
            tsShowGameError.IsChecked = false;
            tsShowNpcDialog.IsChecked = false;
            tsShowNpcAnnounce.IsChecked = false;
            tsShowBossQuotes.IsChecked = false;
            tsShowRecruitment.IsChecked = false;
            tsShowGather.IsChecked = false;
            tsShowMarketSold.IsChecked = false;
            tsShowGilReceive.IsChecked = false;
        }

        private void DeepLAPIListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listView = sender as Wpf.Ui.Controls.ListView;
            ViewModel.SelectedDeeplApiKeyIndex = listView.SelectedIndex;
        }

        private void tbNewDeeplApiKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tbName = sender as System.Windows.Controls.TextBox;
            if (tbName != null && tbName.Text != string.Empty)
            {
                ViewModel.NewDeepLApiKey = tbName.Text;
            }
        }

        private void cbTranslator_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || _isChangingTranslatorSelection) return;

            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            var selectedItem = ViewModel.TranslatorEngine;

            if (selectedItem == Models.Enums.TranslatorEngine.DeepL_API)
            {
                if (IronworksSettings.Instance.TranslatorSettings.DeeplApiKeys.Count == 0)
                {
                    System.Windows.MessageBox.Show(Localizer.GetString("settings.translator.engine.deepl_api.not_exists"));
                    comboBox.SelectedItem = (Models.Enums.TranslatorEngine)0;
                    comboBox.SelectedIndex = 0;
                }
                else
                {
                    UpdateTranslatorTooltip(selectedItem);
                }
            }
            else
            {
                UpdateTranslatorTooltip(selectedItem);
                EnsureLocalTranslatorModelReady(selectedItem, comboBox);
            }
        }

        private void MiLMMTModelOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || _isChangingTranslatorSelection) return;

            UpdateMiLMMTProfileSummary();
            if (ViewModel.TranslatorEngine == Models.Enums.TranslatorEngine.MiLLMT)
            {
                EnsureLocalTranslatorModelReady(ViewModel.TranslatorEngine, cbTranslator);
            }
        }

        private void LocalModelDevicePriority_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || _isChangingDevicePrioritySelection) return;

            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            var selectedPriority = ViewModel.LocalModelDevicePriority;
            if (selectedPriority == LocalModelDevicePriority.Cpu
                || IsDevicePriorityCompatibleWithCurrentGpu(selectedPriority))
            {
                _previousLocalModelDevicePriority = selectedPriority;
                ViewModel.RefreshMiLMMTProfileSummary();
                return;
            }

            var recommendedPriority = GetRecommendedDevicePriority(_lastSystemResourceSnapshot?.VramAdapterName);
            var result = System.Windows.MessageBox.Show(
                string.Format(
                    Localizer.GetString("settings.translator.engine.milmmt.device_mismatch.confirm"),
                    _lastSystemResourceSnapshot?.VramAdapterName ?? "N/A",
                    GetDevicePriorityLabel(recommendedPriority!.Value),
                    GetDevicePriorityLabel(selectedPriority)),
                Localizer.GetString("settings.translator.engine.milmmt.device_mismatch.title"),
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _previousLocalModelDevicePriority = selectedPriority;
                ViewModel.RefreshMiLMMTProfileSummary();
                return;
            }

            RevertLocalModelDevicePriority(comboBox);
        }

        private void OpenSelectedMiLMMTModelDirectory_Click(object sender, RoutedEventArgs e)
        {
            OpenMiLMMTModelDirectory(ViewModel.SelectedMiLMMTProfile);
        }

        private void DeleteSelectedMiLMMTModel_Click(object sender, RoutedEventArgs e)
        {
            DeleteMiLMMTModel(ViewModel.SelectedMiLMMTProfile);
        }

        private void SelectMiLMMTModel_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is MiLMMTModelStorageItem item)
            {
                SelectMiLMMTModel(item.Profile);
            }
        }

        private void OpenMiLMMTModelDirectory_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is MiLMMTModelStorageItem item)
            {
                OpenMiLMMTModelDirectory(item.Profile);
            }
        }

        private void DeleteMiLMMTModel_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is MiLMMTModelStorageItem item)
            {
                DeleteMiLMMTModel(item.Profile);
            }
        }

        private static void OpenMiLMMTModelDirectory(MiLMMTModelProfile profile)
        {
            Directory.CreateDirectory(profile.DirectoryPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                ArgumentList = { profile.DirectoryPath },
            });
        }

        private void DeleteMiLMMTModel(MiLMMTModelProfile profile)
        {
            if (!File.Exists(profile.FilePath))
            {
                UpdateMiLMMTProfileSummary();
                return;
            }

            var result = System.Windows.MessageBox.Show(
                string.Format(
                    Localizer.GetString("settings.translator.engine.milmmt.delete.confirm"),
                    profile.DisplayName),
                Localizer.GetString("settings.translator.engine.milmmt.delete"),
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                File.Delete(profile.FilePath);
                var tempPath = $"{profile.FilePath}.download";
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    string.Format(
                        Localizer.GetString("settings.translator.engine.milmmt.delete.failed"),
                        ex.Message));
            }

            UpdateMiLMMTProfileSummary();
        }

        private void SelectMiLMMTModel(MiLMMTModelProfile profile)
        {
            SelectMiLMMTProfile(profile, File.Exists(profile.FilePath));
            if (ViewModel.TranslatorEngine == Models.Enums.TranslatorEngine.MiLLMT)
            {
                EnsureLocalTranslatorModelReady(ViewModel.TranslatorEngine, cbTranslator);
            }
            else
            {
                UpdateMiLMMTProfileSummary();
            }
        }

        private void EnsureLocalTranslatorModelReady(
            Models.Enums.TranslatorEngine selectedItem,
            ComboBox comboBox)
        {
            if (!RequiresLocalTranslatorModel(selectedItem) || LocalTranslatorModelExists(selectedItem))
            {
                return;
            }

            var result = System.Windows.MessageBox.Show(
                Localizer.GetString("settings.translator.engine.download_model.confirm"),
                Localizer.GetString("settings.translator.engine"),
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                if (selectedItem == Models.Enums.TranslatorEngine.MiLLMT
                    && TrySelectAvailableMiLMMTProfile())
                {
                    UpdateTranslatorTooltip(selectedItem);
                    return;
                }

                RevertTranslatorEngineToDefault(comboBox);
                return;
            }

            var window = selectedItem == Models.Enums.TranslatorEngine.MiLLMT
                ? new InitializationWindow(selectedItem, ViewModel.SelectedMiLMMTProfile)
                : new InitializationWindow(selectedItem);
            window.ShowDialog();
            UpdateMiLMMTProfileSummary();
            RememberCurrentMiLMMTProfileIfAvailable();
        }

        private void RevertTranslatorEngineToDefault(ComboBox comboBox)
        {
            SelectTranslatorEngine(Models.Enums.TranslatorEngine.Papago, comboBox);
        }

        private void SelectTranslatorEngine(
            Models.Enums.TranslatorEngine translatorEngine,
            ComboBox comboBox)
        {
            _isChangingTranslatorSelection = true;
            try
            {
                ViewModel.TranslatorEngine = translatorEngine;
                ViewModel.TranslatorEngineIndex = (int)translatorEngine;
                comboBox.SelectedItem = translatorEngine;
                comboBox.SelectedIndex = (int)translatorEngine;
                UpdateTranslatorTooltip(translatorEngine);
            }
            finally
            {
                _isChangingTranslatorSelection = false;
            }
        }

        private static bool RequiresLocalTranslatorModel(Models.Enums.TranslatorEngine selectedItem)
        {
            return selectedItem is Models.Enums.TranslatorEngine.MiLLMT;
        }

        private bool LocalTranslatorModelExists(Models.Enums.TranslatorEngine selectedItem)
        {
            return selectedItem switch
            {
                Models.Enums.TranslatorEngine.MiLLMT => File.Exists(ViewModel.SelectedMiLMMTProfile.FilePath),
                _ => true,
            };
        }

        private void UpdateTranslatorTooltip(Models.Enums.TranslatorEngine selectedItem)
        {
            if (txtPapagoTooltip == null || txtMiLMMTTooltip == null)
            {
                return;
            }

            txtPapagoTooltip.Visibility = selectedItem == Models.Enums.TranslatorEngine.Papago
                ? Visibility.Visible
                : Visibility.Collapsed;
            txtMiLMMTTooltip.Visibility = selectedItem == Models.Enums.TranslatorEngine.MiLLMT
                ? Visibility.Visible
                : Visibility.Collapsed;
            panelMiLMMTOptions.Visibility = selectedItem == Models.Enums.TranslatorEngine.MiLLMT
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateMiLMMTProfileSummary();
        }

        private void UpdateMiLMMTProfileSummary()
        {
            RememberCurrentMiLMMTProfileIfAvailable();
            ViewModel.RefreshMiLMMTProfileSummary();
        }

        private void InitializeLastAvailableMiLMMTProfile()
        {
            _lastAvailableMiLMMTModelSize = ViewModel.MiLMMTModelSize;
            _lastAvailableMiLMMTQuantization = ViewModel.MiLMMTQuantization;

            if (File.Exists(ViewModel.SelectedMiLMMTProfile.FilePath))
            {
                return;
            }

            var downloadedProfile = MiLMMTModelProfiles.All
                .FirstOrDefault(profile => File.Exists(profile.FilePath));
            if (downloadedProfile != null)
            {
                _lastAvailableMiLMMTModelSize = downloadedProfile.Size;
                _lastAvailableMiLMMTQuantization = downloadedProfile.Quantization;
            }
        }

        private void RememberCurrentMiLMMTProfileIfAvailable()
        {
            if (!File.Exists(ViewModel.SelectedMiLMMTProfile.FilePath))
            {
                return;
            }

            _lastAvailableMiLMMTModelSize = ViewModel.MiLMMTModelSize;
            _lastAvailableMiLMMTQuantization = ViewModel.MiLMMTQuantization;
        }

        private bool TrySelectAvailableMiLMMTProfile()
        {
            var lastProfile = MiLMMTModelProfiles.Get(
                _lastAvailableMiLMMTModelSize,
                _lastAvailableMiLMMTQuantization);
            if (File.Exists(lastProfile.FilePath))
            {
                SelectMiLMMTProfile(lastProfile);
                return true;
            }

            var downloadedProfile = MiLMMTModelProfiles.All
                .FirstOrDefault(profile => File.Exists(profile.FilePath));
            if (downloadedProfile == null)
            {
                return false;
            }

            SelectMiLMMTProfile(downloadedProfile);
            return true;
        }

        private void SelectMiLMMTProfile(MiLMMTModelProfile profile, bool rememberIfAvailable = true)
        {
            _isChangingTranslatorSelection = true;
            try
            {
                ViewModel.MiLMMTModelSize = profile.Size;
                ViewModel.MiLMMTModelSizeIndex = (int)profile.Size;
                ViewModel.MiLMMTQuantization = profile.Quantization;
                ViewModel.MiLMMTQuantizationIndex = (int)profile.Quantization;
                if (rememberIfAvailable)
                {
                    _lastAvailableMiLMMTModelSize = profile.Size;
                    _lastAvailableMiLMMTQuantization = profile.Quantization;
                }
                ViewModel.RefreshMiLMMTProfileSummary();
            }
            finally
            {
                _isChangingTranslatorSelection = false;
            }
        }

        private void UpdateResourceSnapshot()
        {
            if (txtRamUsage == null || txtVramUsage == null)
            {
                return;
            }

            var snapshot = SystemResourceMonitor.GetSnapshot();
            _lastSystemResourceSnapshot = snapshot;
            ViewModel.UpdateMiLMMTResourceSnapshot(snapshot);
            txtVramAdapterName.Text = string.IsNullOrWhiteSpace(snapshot.VramAdapterName)
                ? "GPU: N/A"
                : $"GPU: {snapshot.VramAdapterName}";
            if (snapshot.TotalRamBytes > 0)
            {
                txtRamUsage.Text = FormatUsage(snapshot.UsedRamBytes, snapshot.TotalRamBytes);
                pbRamUsage.Value = GetUsagePercent(snapshot.UsedRamBytes, snapshot.TotalRamBytes);
            }

            if (snapshot.TotalVramBytes is { } totalVram && snapshot.UsedVramBytes is { } usedVram)
            {
                txtVramUsage.Text = FormatUsage(usedVram, totalVram);
                pbVramUsage.Value = GetUsagePercent(usedVram, totalVram);
            }
            else if (snapshot.TotalVramBytes is { } totalVramOnly)
            {
                txtVramUsage.Text = $"? / {ToGiB(totalVramOnly):N1} GB";
                pbVramUsage.Value = 0;
            }
            else if (snapshot.UsedVramBytes is { } usedVramOnly)
            {
                txtVramUsage.Text = $"{ToGiB(usedVramOnly):N1} GB / ?";
                pbVramUsage.Value = 0;
            }
            else
            {
                txtVramUsage.Text = "N/A";
                pbVramUsage.Value = 0;
            }
        }

        private static string FormatUsage(ulong usedBytes, ulong totalBytes)
        {
            return $"{ToGiB(usedBytes):N1} / {ToGiB(totalBytes):N1} GB";
        }

        private static double GetUsagePercent(ulong usedBytes, ulong totalBytes)
        {
            return totalBytes == 0 ? 0 : Math.Min(100, usedBytes * 100d / totalBytes);
        }

        private static double ToGiB(ulong bytes)
        {
            return bytes / 1024d / 1024d / 1024d;
        }

        private bool IsDevicePriorityCompatibleWithCurrentGpu(LocalModelDevicePriority selectedPriority)
        {
            var recommendedPriority = GetRecommendedDevicePriority(_lastSystemResourceSnapshot?.VramAdapterName);
            return recommendedPriority == null || recommendedPriority == selectedPriority;
        }

        private static LocalModelDevicePriority? GetRecommendedDevicePriority(string? adapterName)
        {
            return LocalModelDevicePrioritySelector.GetRecommendedPriority(adapterName);
        }

        private static string GetDevicePriorityLabel(LocalModelDevicePriority priority)
        {
            return priority switch
            {
                LocalModelDevicePriority.Cuda => "CUDA",
                LocalModelDevicePriority.Vulkan => "Vulkan",
                _ => "CPU",
            };
        }

        private void RevertLocalModelDevicePriority(ComboBox comboBox)
        {
            _isChangingDevicePrioritySelection = true;
            try
            {
                ViewModel.LocalModelDevicePriority = _previousLocalModelDevicePriority;
                ViewModel.LocalModelDevicePriorityIndex = (int)_previousLocalModelDevicePriority;
                comboBox.SelectedItem = _previousLocalModelDevicePriority;
                comboBox.SelectedIndex = (int)_previousLocalModelDevicePriority;
                ViewModel.RefreshMiLMMTProfileSummary();
            }
            finally
            {
                _isChangingDevicePrioritySelection = false;
            }
        }
    }
#pragma warning restore CS8602
}
