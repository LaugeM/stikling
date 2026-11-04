# The Help page and the front page

`Pages/Help.razor` has a few answers for each part of the app. The text is in `Resources/Help.resx` and `Help.da.resx`, and the page lists the keys, so a new answer is a key in both files and a line on the page.

The deploy also renders the page to plain HTML for search engines, and into `llms.txt` for AI tools (`tools/static-pages/render.cs`), so it has to render without a browser: anything that calls JavaScript goes in `OnAfterRenderAsync`.

The front page answers a few of the same questions, so when an answer changes, check those too. It is `StaticPages/FrontPage.razor`, with its text in `Resources/FrontPage.resx` and `FrontPage.da.resx`, and the deploy puts it into `index.html` (the `wwwroot/index.html` in the repository has no front page of its own).
