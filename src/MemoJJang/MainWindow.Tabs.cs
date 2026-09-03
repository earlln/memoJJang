using System.Windows;
using System.Windows.Controls;
using MemoJJang.Models;

namespace MemoJJang;

/// <summary>탭 머리글 오른쪽 클릭 메뉴.</summary>
public partial class MainWindow
{
    private enum TabMenuAction
    {
        NewTab,
        Close,
        CloseOthers,
        CloseRight,
        CloseLeft
    }

    private sealed record TabMenuContext(DocumentTab Document, TabMenuAction Action);

    private ContextMenu CreateTabContextMenu(DocumentTab document)
    {
        var menu = new ContextMenu();

        menu.Items.Add(CreateTabMenuItem("새 탭(_T)", "Ctrl+T", document, TabMenuAction.NewTab));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateTabMenuItem("탭 닫기(_C)", "Ctrl+W", document, TabMenuAction.Close));
        menu.Items.Add(CreateTabMenuItem("다른 탭 모두 닫기(_O)", null, document, TabMenuAction.CloseOthers));
        menu.Items.Add(CreateTabMenuItem("오른쪽 탭 모두 닫기(_R)", null, document, TabMenuAction.CloseRight));
        menu.Items.Add(CreateTabMenuItem("왼쪽 탭 모두 닫기(_L)", null, document, TabMenuAction.CloseLeft));

        menu.Opened += TabContextMenu_Opened;
        return menu;
    }

    private MenuItem CreateTabMenuItem(string header, string? gesture, DocumentTab document, TabMenuAction action)
    {
        var item = new MenuItem
        {
            Header = header,
            Tag = new TabMenuContext(document, action)
        };

        if (!string.IsNullOrEmpty(gesture))
        {
            item.InputGestureText = gesture;
        }

        item.Click += TabMenuItem_Click;
        return item;
    }

    /// <summary>메뉴를 열 때마다 현재 탭 위치를 보고 항목의 사용 가능 여부를 갱신한다.</summary>
    private void TabContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
        {
            return;
        }

        var ordered = DocumentsInTabOrder();

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Tag is not TabMenuContext context)
            {
                continue;
            }

            var index = ordered.IndexOf(context.Document);

            item.IsEnabled = context.Action switch
            {
                TabMenuAction.NewTab => true,
                TabMenuAction.Close => true,
                TabMenuAction.CloseOthers => ordered.Count > 1,
                TabMenuAction.CloseRight => index >= 0 && index < ordered.Count - 1,
                TabMenuAction.CloseLeft => index > 0,
                _ => true
            };
        }
    }

    private void TabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: TabMenuContext context })
        {
            return;
        }

        var ordered = DocumentsInTabOrder();
        var index = ordered.IndexOf(context.Document);

        switch (context.Action)
        {
            case TabMenuAction.NewTab:
                NewTab_Click(sender, e);
                break;

            case TabMenuAction.Close:
                CloseDocument(context.Document);
                break;

            case TabMenuAction.CloseOthers:
                CloseDocuments(ordered.Where(d => !ReferenceEquals(d, context.Document)));
                break;

            case TabMenuAction.CloseRight when index >= 0:
                // 오른쪽 끝에서부터 닫아야 중간에 취소해도 남는 탭이 자연스럽다.
                CloseDocuments(ordered.Skip(index + 1).Reverse());
                break;

            case TabMenuAction.CloseLeft when index > 0:
                CloseDocuments(ordered.Take(index).Reverse());
                break;
        }

        UpdateTitle();
        UpdateStatusBar();
        RefreshMarkdownMenu();
    }

    /// <summary>탭에 배치된 순서대로 문서 목록을 돌려준다.</summary>
    private List<DocumentTab> DocumentsInTabOrder()
        => Tabs.Items.OfType<TabItem>()
            .Select(tab => tab.Tag)
            .OfType<DocumentTab>()
            .ToList();

    /// <summary>여러 탭을 차례로 닫는다. 저장 확인에서 취소하면 거기서 멈춘다.</summary>
    private void CloseDocuments(IEnumerable<DocumentTab> targets)
    {
        foreach (var document in targets.ToList())
        {
            if (!CloseDocument(document))
            {
                break;
            }
        }
    }
}
