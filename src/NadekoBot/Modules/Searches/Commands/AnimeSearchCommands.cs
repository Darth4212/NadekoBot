﻿using AngleSharp;
using AngleSharp.Dom.Html;
using Discord;
using Discord.Commands;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using NadekoBot.Services.Searches;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class AnimeSearchCommands : NadekoSubmodule
        {
            private readonly AnimeSearchService _service;

            public AnimeSearchCommands(AnimeSearchService service)
            {
                _service = service;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public async Task Mal([Remainder] string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return;

                var fullQueryLink = "https://myanimelist.net/profile/" + name;

                var config = Configuration.Default.WithDefaultLoader();
                var document = await BrowsingContext.New(config).OpenAsync(fullQueryLink);

                var imageElem = document.QuerySelector("body > div#myanimelist > div.wrapper > div#contentWrapper > div#content > div.content-container > div.container-left > div.user-profile > div.user-image > img");
                var imageUrl = ((IHtmlImageElement)imageElem)?.Source ?? "http://icecream.me/uploads/870b03f36b59cc16ebfe314ef2dde781.png";

                var stats = document.QuerySelectorAll("body > div#myanimelist > div.wrapper > div#contentWrapper > div#content > div.content-container > div.container-right > div#statistics > div.user-statistics-stats > div.stats > div.clearfix > ul.stats-status > li > span").Select(x => x.InnerHtml).ToList();

                var favorites = document.QuerySelectorAll("div.user-favorites > div.di-tc");

                var favAnime = GetText("anime_no_fav");
                if (favorites[0].QuerySelector("p") == null)
                    favAnime = string.Join("\n", favorites[0].QuerySelectorAll("ul > li > div.di-tc.va-t > a")
                       .Shuffle()
                       .Take(3)
                       .Select(x =>
                       {
                           var elem = (IHtmlAnchorElement)x;
                           return $"[{elem.InnerHtml}]({elem.Href})";
                       }));

                //var favManga = "No favorite manga yet.";
                //if (favorites[1].QuerySelector("p") == null)
                //    favManga = string.Join("\n", favorites[1].QuerySelectorAll("ul > li > div.di-tc.va-t > a")
                //       .Take(3)
                //       .Select(x =>
                //       {
                //           var elem = (IHtmlAnchorElement)x;
                //           return $"[{elem.InnerHtml}]({elem.Href})";
                //       }));

                var info = document.QuerySelectorAll("ul.user-status:nth-child(3) > li.clearfix")
                    .Select(x => Tuple.Create(x.Children[0].InnerHtml, x.Children[1].InnerHtml))
                    .ToList();

                var daysAndMean = document.QuerySelectorAll("div.anime:nth-child(1) > div:nth-child(2) > div")
                    .Select(x => x.TextContent.Split(':').Select(y => y.Trim()).ToArray())
                    .ToArray();

                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(GetText("mal_profile", name))
                    .AddField(efb => efb.WithName("💚 " + GetText("watching")).WithValue(stats[0]).WithIsInline(true))
                    .AddField(efb => efb.WithName("💙 " + GetText("completed")).WithValue(stats[1]).WithIsInline(true));
                if (info.Count < 3)
                    embed.AddField(efb => efb.WithName("💛 " + GetText("on_hold")).WithValue(stats[2]).WithIsInline(true));
                embed
                    .AddField(efb => efb.WithName("💔 " + GetText("dropped")).WithValue(stats[3]).WithIsInline(true))
                    .AddField(efb => efb.WithName("⚪ " + GetText("plan_to_watch")).WithValue(stats[4]).WithIsInline(true))
                    .AddField(efb => efb.WithName("🕐 " + daysAndMean[0][0]).WithValue(daysAndMean[0][1]).WithIsInline(true))
                    .AddField(efb => efb.WithName("📊 " + daysAndMean[1][0]).WithValue(daysAndMean[1][1]).WithIsInline(true))
                    .AddField(efb => efb.WithName(MalInfoToEmoji(info[0].Item1) + " " + info[0].Item1).WithValue(info[0].Item2.TrimTo(20)).WithIsInline(true))
                    .AddField(efb => efb.WithName(MalInfoToEmoji(info[1].Item1) + " " + info[1].Item1).WithValue(info[1].Item2.TrimTo(20)).WithIsInline(true));
                if (info.Count > 2)
                    embed.AddField(efb => efb.WithName(MalInfoToEmoji(info[2].Item1) + " " + info[2].Item1).WithValue(info[2].Item2.TrimTo(20)).WithIsInline(true));
                //if(info.Count > 3)
                //    embed.AddField(efb => efb.WithName(MalInfoToEmoji(info[3].Item1) + " " + info[3].Item1).WithValue(info[3].Item2).WithIsInline(true))
                embed
                    .WithDescription($@"
** https://myanimelist.net/animelist/{ name } **

**{GetText("top_3_fav_anime")}**
{favAnime}"

//**[Manga List](https://myanimelist.net/mangalist/{name})**
//💚`Reading:` {stats[5]}
//💙`Completed:` {stats[6]}
//💔`Dropped:` {stats[8]}
//⚪`Plan to read:` {stats[9]}

//**Top 3 Favorite Manga:**
//{favManga}"

)
                    .WithUrl(fullQueryLink)
                    .WithImageUrl(imageUrl);

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            private static string MalInfoToEmoji(string info) {
                info = info.Trim().ToLowerInvariant();
                switch (info)
                {
                    case "gender":
                        return "🚁";
                    case "location":
                        return "🗺";
                    case "last online":
                        return "👥";
                    case "birthday":
                        return "📆";
                    default:
                        return "❔";
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(1)]
            public Task Mal(IGuildUser usr) => Mal(usr.Username);

            [NadekoCommand, Usage, Description, Aliases]
            public async Task Anime([Remainder] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;

                var animeData = await _service.GetAnimeData(query).ConfigureAwait(false);

                if (animeData == null)
                {
                    await ReplyErrorLocalized("failed_finding_anime").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithColor(NadekoBot.OkColor)
                    .WithDescription(animeData.Synopsis.Replace("<br>", Environment.NewLine))
                    .WithTitle(animeData.title_english)
                    .WithUrl(animeData.Link)
                    .WithImageUrl(animeData.image_url_lge)
                    .AddField(efb => efb.WithName(GetText("episodes")).WithValue(animeData.total_episodes.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName(GetText("status")).WithValue(animeData.AiringStatus.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName(GetText("genres")).WithValue(String.Join(",\n", animeData.Genres)).WithIsInline(true))
                    .WithFooter(efb => efb.WithText(GetText("score") + " " + animeData.average_score + " / 100"));
                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Manga([Remainder] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;

                var mangaData = await _service.GetMangaData(query).ConfigureAwait(false);

                if (mangaData == null)
                {
                    await ReplyErrorLocalized("failed_finding_manga").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithColor(NadekoBot.OkColor)
                    .WithDescription(mangaData.Synopsis.Replace("<br>", Environment.NewLine))
                    .WithTitle(mangaData.title_english)
                    .WithUrl(mangaData.Link)
                    .WithImageUrl(mangaData.image_url_lge)
                    .AddField(efb => efb.WithName(GetText("chapters")).WithValue(mangaData.total_chapters.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName(GetText("status")).WithValue(mangaData.publishing_status.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName(GetText("genres")).WithValue(String.Join(",\n", mangaData.Genres)).WithIsInline(true))
                    .WithFooter(efb => efb.WithText(GetText("score") + " " + mangaData.average_score + " / 100"));

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }
    }
}