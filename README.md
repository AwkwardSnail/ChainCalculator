# Chain Calc

A calculator laid out like a tiny spreadsheet. Type up to five numbers, give
each one its own operation (+ − × ÷), and watch the running result build up
line by line.

Made by [aWkward sn@ail](https://awkwardsnail.com.au): calm, simple tools that
stay the way you left them.

## Get it

- **Windows:** coming soon to the Microsoft Store.
- **In your browser:** [fdimer.github.io/chain-calc](https://fdimer.github.io/chain-calc/)
- **Build it yourself (free):** see below.

## Building

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```
dotnet run --project src/ChainCalculator
dotnet test
```

`packaging\pack.ps1` builds the MSIX package (needs the Windows SDK).
The browser version is the single file `index.html`.

## Privacy

Chain Calc collects no data, works offline and never contacts a server.
See the [privacy policy](https://awkwardsnail.com.au/privacy.html).

## Licence

The code is under the [MIT licence](LICENSE), so you're welcome to fork it.
The Awkward Snail name and icon are not covered by the licence: please don't
use them for your fork.
