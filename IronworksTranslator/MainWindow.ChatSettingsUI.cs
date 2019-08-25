using FontAwesome.WPF;
using IronworksTranslator.Core;
using System.Windows;
using System.Windows.Controls;

namespace IronworksTranslator
{
    /// <summary>
    /// 채팅 관련 설정 UI 코드들 중에 간단한 것들을 모아뒀음
    /// </summary>
    public partial class MainWindow : Window
    {
        private void TellComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Tell, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void SayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Say, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void YellComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Yell, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void ShoutComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Shout, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void PartyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Party, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void AllianceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Alliance, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void TellShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Tell);
        }

        private void SayShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Say);
        }

        private void YellShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Yell);
        }

        private void ShoutShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Shout);
        }

        private void PartyShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Party);
        }

        private void AllianceShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Alliance);
        }

        private void CommunityGroupHideButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayCommunityGroup(false, FontAwesomeIcon.EyeSlash);
        }

        private void CommunityGroupShowButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayCommunityGroup(true, FontAwesomeIcon.Eye);
        }

        private void FreeCompanyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.FreeCompany, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void NoviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Novice, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void FreeCompanyShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.FreeCompany);
        }

        private void NoviceShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Novice);
        }

        private void LinkShell1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell1, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell2, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell3ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell3, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell4ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell4, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell5ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell5, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell6ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell6, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell7ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell7, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell8ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell8, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell1ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell1);
        }

        private void LinkShell2ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell2);
        }


        private void LinkShell3ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell3);
        }


        private void LinkShell4ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell4);
        }


        private void LinkShell5ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell5);
        }

        private void LinkShell6ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell6);
        }

        private void LinkShell7ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell7);
        }

        private void LinkShell8ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell8);
        }

        private void CWLinkShell1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell1, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell2, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell3ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell3, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell4ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell4, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell5ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell5, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell6ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell6, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell7ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell7, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell8ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell8, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell1ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell1);
        }

        private void CWLinkShell2ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell2);
        }

        private void CWLinkShell3ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell3);
        }

        private void CWLinkShell4ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell4);
        }

        private void CWLinkShell5ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell5);
        }

        private void CWLinkShell6ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell6);
        }

        private void CWLinkShell7ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell7);
        }

        private void CWLinkShell8ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell8);
        }
    }
}
