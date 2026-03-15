# LDERC
# Local Dollar Exchange Rate Checker

A small Windows desktop app for tracking live USD exchange rates and converting a typed USD amount into popular world currencies.

## Features

- Live USD exchange rates with manual refresh
- Currency calculator for a custom USD amount
- Search by code, English name, or Russian name
- Alphabetical currency list
- Dark desktop UI with a native Windows executable

## Data source

The app fetches rates from:

- `https://open.er-api.com/v6/latest/USD`

## Privacy and safety

- The app only uses the text typed into the calculator and search box.
- It does not read personal files, browser data, passwords, or system settings.
- It does not execute user commands or provide remote system access.
- It opens the project GitHub link in the default browser if the user clicks it.

More detail is available in Readme.txt

## Files

- `LocalDollarExchangeRateChecker.cs`: app source code
- `build.ps1`: local build script
- `LocalDollarExchangeRateChecker.ico`: app icon
- `LocalDollarExchangeRateChecker.exe`: compiled Windows app

## Build

This project is built with the included PowerShell script
