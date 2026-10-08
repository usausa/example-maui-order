// ReSharper disable RedundantUsingDirective.Global
#pragma warning disable
global using System;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.Globalization;
global using System.IO;
global using System.Linq;
global using System.Reactive.Disposables;
global using System.Reactive.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Input;

global using MauiComponents;

global using Microsoft.Extensions.Logging;

global using Smart;
global using Smart.ComponentModel;
global using Smart.Maui;
global using Smart.Maui.Input;
global using Smart.Maui.Messaging;
global using Smart.Maui.ViewModels;
global using Smart.Mvvm;
global using Smart.Mvvm.Messaging;
global using Smart.Mvvm.ViewModels;
global using Smart.Navigation;
global using Smart.Navigation.Attributes;
global using Smart.Navigation.Plugins.Parameter;
global using Smart.Reactive;

global using TableOrder.Client;
global using TableOrder.Contract;
global using TableOrder.Contract.Calls;
global using TableOrder.Contract.Devices;
global using TableOrder.Contract.Menu;
global using TableOrder.Contract.Orders;
global using TableOrder.Contract.Serving;
global using TableOrder.Contract.Stores;
global using TableOrder.Contract.Visits;
global using TableOrder.Domain;
global using TableOrder.Domain.Enums;

global using TableOrder.HallApp;
global using TableOrder.HallApp.Modules.Helpers;
global using TableOrder.HallApp.Resources.Strings;
global using TableOrder.HallApp.State;
global using TableOrder.HallApp.Usecase;

global using TableOrder.Terminal;
global using TableOrder.Terminal.Models;
global using TableOrder.Terminal.Modules;
global using TableOrder.Terminal.Resources.Strings;
global using TableOrder.Terminal.State;
global using TableOrder.Terminal.Usecase;
