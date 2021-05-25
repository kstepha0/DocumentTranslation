﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Media;
using DocumentTranslationService.Core;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DocumentTranslation.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ViewModel ViewModel;
        private PerLanguageData perLanguageData = new();
        private int charactersCharged;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel viewModel = new();
            AppSettingsSetter.SettingsReadComplete += AppSettingsSetter_SettingsReadComplete;
            ViewModel = viewModel;
            toLanguageBox.ItemsSource = ViewModel.ToLanguageList;
            fromLanguageBox.ItemsSource = ViewModel.FromLanguageList;
            toLanguageBoxDocuments.ItemsSource = ViewModel.ToLanguageList;
            fromLanguageBoxDocuments.ItemsSource = ViewModel.FromLanguageList;
            CategoryDocumentsBox.ItemsSource = ViewModel.categories.MyCategoryList;
            CategoryTextBox.ItemsSource = ViewModel.categories.MyCategoryList;
        }

        private void AppSettingsSetter_SettingsReadComplete(object sender, EventArgs e)
        {
            EnableTabs();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await ViewModel.Initialize();
            }
            catch (ArgumentNullException ex)
            {
                SettingsTab.IsSelected = true;
                TranslateDocumentsTab.IsEnabled = false;
                if (ex.ParamName == "SubscriptionKey") TranslateTextTab.IsEnabled = false;
            }
            toLanguageBox.SelectedValue = ViewModel.UISettings.lastToLanguage;
            if (ViewModel.UISettings.lastFromLanguage is not null)
                fromLanguageBox.SelectedValue = ViewModel.UISettings.lastFromLanguage;
            else fromLanguageBox.SelectedIndex = 0;
            toLanguageBoxDocuments.SelectedValue = ViewModel.UISettings.lastToLanguageDocuments;
            if (ViewModel.UISettings.lastFromLanguageDocuments is not null)
                fromLanguageBoxDocuments.SelectedValue = ViewModel.UISettings.lastFromLanguageDocuments;
            else fromLanguageBoxDocuments.SelectedIndex = 0;
            CategoryDocumentsBox.SelectedValue = ViewModel.UISettings.lastCategoryDocuments;
            CategoryTextBox.SelectedValue = ViewModel.UISettings.lastCategoryText;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ViewModel.UISettings.lastToLanguage = toLanguageBox.SelectedValue as string;
            ViewModel.UISettings.lastFromLanguage = fromLanguageBox.SelectedValue as string;
            ViewModel.UISettings.lastToLanguageDocuments = toLanguageBoxDocuments.SelectedValue as string;
            ViewModel.UISettings.lastFromLanguageDocuments = fromLanguageBoxDocuments.SelectedValue as string;
            if (CategoryDocumentsBox.SelectedItem is not null) ViewModel.UISettings.lastCategoryDocuments = ((MyCategory)CategoryDocumentsBox.SelectedItem).Name ?? string.Empty;
            else ViewModel.UISettings.lastCategoryDocuments = null;
            if (CategoryTextBox.SelectedItem is not null) ViewModel.UISettings.lastCategoryText = ((MyCategory)CategoryTextBox.SelectedItem).Name ?? string.Empty;
            else ViewModel.UISettings.lastCategoryText = null;
            await ViewModel.SaveAsync();
        }

        private async void TabItemAuthentication_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.GetAzureRegions();
            subscriptionKey.Password = ViewModel.Settings.SubscriptionKey;
            region.ItemsSource = ViewModel.AzureRegions;
            region.SelectedValue = ViewModel.Settings.AzureRegion;
            storageConnectionString.Text = ViewModel.Settings.ConnectionStrings.StorageConnectionString;
            resourceName.Text = ViewModel.Settings.AzureResourceName;
        }

        private async void TabItemAuthentication_Unloaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.SaveAsync();
        }

        private async void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            outputBox.Text = await ViewModel.TranslateTextAsync(inputBox.Text, fromLanguageBox.SelectedValue as string, toLanguageBox.SelectedValue as string);
        }

        private async void DocumentBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            OpenFileDialog openFileDialog = new() { RestoreDirectory = true, CheckFileExists = true, Multiselect = true };
            if (ViewModel.UISettings.lastDocumentsFolder is not null) openFileDialog.InitialDirectory = ViewModel.UISettings.lastDocumentsFolder;
            openFileDialog.Filter = await this.ViewModel.GetDocumentExtensionsFilter();
            openFileDialog.ShowDialog();
            foreach (var filename in openFileDialog.FileNames)
                ViewModel.FilesToTranslate.Add(filename);
            FilesListBox.ItemsSource = ViewModel.FilesToTranslate;
            if ((ViewModel.FilesToTranslate.Count > 0) && (TargetListBox.Items.Count > 0)) translateDocumentsButton.IsEnabled = true;
            return;
        }

        private void ResetUI()
        {
            ProgressBar.Value = 0;
            StatusBarText1.Text = string.Empty;
            StatusBarText2.Text = string.Empty;
            CancelButton.IsEnabled = false;
            CancelButton.Background = Brushes.Gray;
            CancelButton.Visibility = Visibility.Visible;
            TargetOpenButton.Visibility = Visibility.Hidden;
        }

        private async void GlossariesBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            OpenFileDialog openFileDialog = new() { RestoreDirectory = true, CheckFileExists = true, Multiselect = true };
            openFileDialog.Filter = await this.ViewModel.GetGlossaryExtensionsFilter();
            if (perLanguageData.lastGlossariesFolder is not null) openFileDialog.InitialDirectory = perLanguageData.lastGlossariesFolder;
            openFileDialog.ShowDialog();
            foreach (var filename in openFileDialog.FileNames)
                ViewModel.GlossariesToUse.Add(filename);
            GlossariesListBox.ItemsSource = ViewModel.GlossariesToUse;
            return;
        }

        private void TargetOpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrEmpty(ViewModel.TargetFolder)) Process.Start("explorer.exe", ViewModel.TargetFolder);
        }

        private void TargetBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            List<string> items = new();
            FolderBrowserDialog folderBrowserDialog = new();
            if ((perLanguageData is not null) && (perLanguageData.lastTargetFolder is not null)) folderBrowserDialog.SelectedPath = perLanguageData.lastTargetFolder;
            folderBrowserDialog.ShowDialog();
            ViewModel.TargetFolder = folderBrowserDialog.SelectedPath;
            items.Add(ViewModel.TargetFolder);
            TargetListBox.ItemsSource = items;
            if ((ViewModel.FilesToTranslate.Count > 0) && (TargetListBox.Items.Count > 0)) translateDocumentsButton.IsEnabled = true;
        }

        private void DocumentsTranslateButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            CancelButton.IsEnabled = true;
            ProgressBar.IsIndeterminate = true;
            ViewModel.UISettings.lastDocumentsFolder = Path.GetDirectoryName(ViewModel.FilesToTranslate[0]);
            PerLanguageData perLanguageData = new();
            if ((ViewModel.GlossariesToUse.Count > 0) && (ViewModel.GlossariesToUse[0] is not null))
            {
                perLanguageData.lastGlossariesFolder = Path.GetDirectoryName(ViewModel.GlossariesToUse[0]);
                perLanguageData.lastGlossary = ViewModel.GlossariesToUse[0];
            }
            perLanguageData.lastTargetFolder = ViewModel.TargetFolder;
            if (ViewModel.UISettings.PerLanguageFolders.ContainsKey(toLanguageBoxDocuments.SelectedValue as string))
            {
                ViewModel.UISettings.PerLanguageFolders.Remove(toLanguageBoxDocuments.SelectedValue as string);
            }
            ViewModel.UISettings.PerLanguageFolders.Add(toLanguageBoxDocuments.SelectedValue as string, perLanguageData);
            _ = ViewModel.SaveAsync();
            if (CategoryDocumentsBox.SelectedItem is not null) ViewModel.documentTranslationService.Category = ((MyCategory)CategoryDocumentsBox.SelectedItem).ID;
            else ViewModel.documentTranslationService.Category = null;
            DocumentTranslationBusiness documentTranslationBusiness = new(ViewModel.documentTranslationService);
            documentTranslationBusiness.OnUploadComplete += DocumentTranslationBusiness_OnUploadComplete;
            documentTranslationBusiness.OnStatusUpdate += DocumentTranslationBusiness_OnStatusUpdate;
            documentTranslationBusiness.OnDownloadComplete += DocumentTranslationBusiness_OnDownloadComplete;
            _ = documentTranslationBusiness.RunAsync(ViewModel.FilesToTranslate, toLanguageBoxDocuments.SelectedValue as string, ViewModel.GlossariesToUse, ViewModel.TargetFolder);
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 1;
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            StatusBarText1.Text = "Canceling...";
            CancelButton.IsEnabled = false;
            CancelButton.Background = Brushes.Red;
            try
            {
                await ViewModel.documentTranslationService.CancelRunAsync();
            }
            catch (UriFormatException) { }
            StatusBarText1.Text = "Canceled";
        }

        private void DocumentTranslationBusiness_OnUploadComplete(object sender, (int, long) e)
        {
            ProgressBar.Value = 10;
            StatusBarText1.Text = "Documents uploaded";
        }

        private void DocumentTranslationBusiness_OnStatusUpdate(object sender, StatusResponse e)
        {
            if (e.error is not null)
                if (!string.IsNullOrEmpty(e.error.code))
                {
                    StatusBarText1.Text = e.error.code;
                    StatusBarText2.Text = e.error.message;
                    ProgressBar.Value = 0;
                    ProgressBar.IsIndeterminate = false;
                    CancelButton.IsEnabled = false;
                    CancelButton.Visibility = Visibility.Hidden;
                    return;
                }
            CancelButton.Background = Brushes.LightGray;
            StatusBarText1.Text = e.status;
            StringBuilder statusText = new();
            if (e.summary.inProgress > 0) statusText.Append("In progress: " + e.summary.inProgress + '\t');
            if (e.summary.notYetStarted > 0) statusText.Append("Waiting: " + e.summary.notYetStarted + '\t');
            if (e.summary.success > 0) statusText.Append("Completed: " + e.summary.success + '\t');
            if (e.summary.failed > 0) statusText.Append("Failed: " + e.summary.failed + '\t');
            if (e.summary.totalCharacterCharged > 0) statusText.Append("Characters charged: " + e.summary.totalCharacterCharged);
            ProgressBar.Value = 10 + ((e.summary.inProgress / ViewModel.FilesToTranslate.Count) * 0.2) + ((e.summary.success + e.summary.failed) / ViewModel.FilesToTranslate.Count * 0.85);
            StatusBarText2.Text = statusText.ToString();
            charactersCharged = e.summary.totalCharacterCharged;
        }


        private void DocumentTranslationBusiness_OnDownloadComplete(object sender, (int, long) e)
        {
            ProgressBar.Value = 100;
            StatusBarText1.Text = "Done";
            StatusBarText2.Text = $"{e.Item2} bytes in {e.Item1} document(s) translated \t";
            if (charactersCharged > 0) StatusBarText2.Text += $" |\t{charactersCharged} characters charged";
            CancelButton.IsEnabled = false;
            CancelButton.Visibility = Visibility.Hidden;
            TargetOpenButton.Visibility = Visibility.Visible;
        }

        private void ToLanguageBoxDocuments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string langCode = toLanguageBoxDocuments.SelectedValue as string;
            if (ViewModel.UISettings.PerLanguageFolders is not null) ViewModel.UISettings.PerLanguageFolders.TryGetValue(langCode, out perLanguageData);
            if ((perLanguageData is not null) && (perLanguageData.lastGlossary is not null)) ViewModel.GlossariesToUse.Add(perLanguageData.lastGlossary);
        }

        private async void EnableTabs()
        {
            await Task.Delay(10);
            TranslateTextTab.IsEnabled = true;
            TranslateDocumentsTab.IsEnabled = true;
            if (string.IsNullOrEmpty(ViewModel.Settings.SubscriptionKey)) { TranslateDocumentsTab.IsEnabled = false; TranslateTextTab.IsEnabled = false; return; }
            if (string.IsNullOrEmpty(ViewModel.Settings.ConnectionStrings.StorageConnectionString)) TranslateDocumentsTab.IsEnabled = false;
            if (string.IsNullOrEmpty(ViewModel.Settings.AzureRegion)) TranslateTextTab.IsEnabled = false;
            if (string.IsNullOrEmpty(ViewModel.Settings.AzureResourceName)) TranslateDocumentsTab.IsEnabled = false;
        }

        private void SubscriptionKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ViewModel.Settings.SubscriptionKey = subscriptionKey.Password;
        }

        private void Region_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void ResourceName_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.Settings.AzureResourceName = resourceName.Text;
        }

        private void StorageConnection_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.Settings.ConnectionStrings.StorageConnectionString = storageConnectionString.Text;
        }
        private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SavedSettingsText.Visibility = Visibility.Visible;
            _ = ViewModel.SaveAsync();
            EnableTabs();
            await Task.Delay(500);
            SavedSettingsText.Visibility = Visibility.Hidden;
        }

        private void CategoriesTab_Loaded(object sender, RoutedEventArgs e)
        {
            CategoriesGridView.AllowUserToAddRows = true;
            CategoriesGridView.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            CategoriesGridView.DataSource = ViewModel.categories.MyCategoryList;
            CategoriesGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            CategoriesGridView.Columns[0].FillWeight = 2;
            CategoriesGridView.Columns[0].HeaderText = "Name";
            CategoriesGridView.Columns[1].FillWeight = 3;
            CategoriesGridView.Columns[1].HeaderText = "Custom Translator Category ID";
        }

        private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddCategory(CategoriesGridView.SelectedCells);
        }

        private void DeleteCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DeleteCategory(CategoriesGridView.SelectedCells);
        }

        private async void SaveCategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            SavedCategoriesText.Visibility = Visibility.Visible;
            CategoriesGridView.EndEdit();
            ViewModel.SaveCategories();
            await Task.Delay(500);
            SavedCategoriesText.Visibility = Visibility.Hidden;
        }

        private void CategoryDocumentsClearButton_Click(object sender, RoutedEventArgs e)
        {
            CategoryDocumentsBox.SelectedItem = null;
            CategoryDocumentsClearButton.Visibility = Visibility.Hidden;
        }

        private void CategoryTextClearButton_Click(object sender, RoutedEventArgs e)
        {
            CategoryTextBox.SelectedItem = null;
            CategoryTextClearButton.Visibility = Visibility.Hidden;
        }

        private void CategoryTextBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryTextBox.SelectedValue is not null) CategoryTextClearButton.Visibility = Visibility.Visible;
            else CategoryTextClearButton.Visibility = Visibility.Hidden;
        }

        private void CategoryDocumentsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryDocumentsBox.SelectedValue is not null) CategoryDocumentsClearButton.Visibility = Visibility.Visible;
            else CategoryDocumentsClearButton.Visibility = Visibility.Hidden;
        }

    }
}
