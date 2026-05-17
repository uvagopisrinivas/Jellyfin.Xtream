const url = (name) =>
  ApiClient.getUrl("configurationpage", {
    name,
  });
const tab = (name) => '/configurationpage?name=' + name + '.html';

$(document).ready(() => {
  const style = document.createElement('link');
  style.rel = 'stylesheet';
  style.href = url('Xtream.css')
  document.head.appendChild(style);
});

const htmlExpand = document.createElement('span');
htmlExpand.ariaHidden = true;
htmlExpand.classList.add('material-icons', 'expand_more');

const createItemRow = (item, state, update) => {
  const tr = document.createElement('tr');
  tr.dataset['itemId'] = item.Id;

  let td = document.createElement('td');
  const checkbox = document.createElement('input');
  checkbox.type = 'checkbox';
  checkbox.checked = state;
  checkbox.onchange = update;
  td.appendChild(checkbox);
  tr.appendChild(td);

  td = document.createElement('td');
  const label = document.createElement('label');
  label.innerText = item.Number ? `${item.Number}. ${item.Name}` : item.Name;
  td.appendChild(label);
  tr.appendChild(td);

  td = document.createElement('td');
  if (item.HasCatchup) {
    td.title = `Catch-up supported for ${item.CatchupDuration} days.`;

    let span = document.createElement('span');
    span.innerText = item.CatchupDuration;
    td.appendChild(span);

    span = document.createElement('span');
    span.ariaHidden = true;
    span.classList.add('material-icons', 'timer');
    td.appendChild(span);
  }
  tr.appendChild(td);

  return tr;
}

const languagePatterns = {
  'Telugu': /[\(\[, ]tel(?:ugu)?[\)\], .\]]/i,
  'Hindi': /[\(\[, ]hin(?:di)?[\)\], .\]]/i,
  'Tamil': /[\(\[, ]tam(?:il)?[\)\], .\]]/i,
  'English': /[\(\[, ]eng(?:lish)?[\)\], .\]]/i,
  'Kannada': /[\(\[, ]kan(?:nada)?[\)\], .\]]/i,
  'Malayalam': /[\(\[, ]mal(?:ayalam)?[\)\], .\]]/i,
};

const itemMatchesLanguage = (name, lang) => {
  if (lang === 'All') return true;
  const pattern = languagePatterns[lang];
  // Pad the name so boundary patterns match at start/end too
  return pattern && pattern.test(' ' + name + ' ');
};

const populateItemsTable = (wrapper, table, items) => {
  for (let i = 0; i < items.length; ++i) {
    const item = items[i];
    const live = wrapper.live;
    const state = Array.isArray(live) && (live.length === 0 || live.includes(item.Id));
    const row = createItemRow(item, state, (e) => {
      let live = wrapper.live;
      if (e.target.checked) {
        if (!Array.isArray(live)) {
          live = [item.Id];
        } else if (live.length === 0) {
          // Already all selected, nothing to do
          return;
        } else {
          live = [...live, item.Id];
        }
        if (items.every(s => live.includes(s.Id))) {
          live = [];
        }
      } else {
        if (!Array.isArray(live)) {
          return;
        }
        if (live.length === 0) {
          live = items.map(s => s.Id);
        }
        live = live.filter(id => id != item.Id);
        if (live.length === 0) {
          live = undefined;
        }
      }
      wrapper.live = live;
    });
    table.appendChild(row);
  }
}

const createLanguageFilter = (wrapper, table, items, categoryCheckbox) => {
  const container = document.createElement('span');
  container.classList.add('language-filter');

  const select = document.createElement('select');
  select.style.cssText = 'margin-left:8px;padding:2px 4px;font-size:0.85em;background:#1c1c1e;color:#ccc;border:1px solid #444;border-radius:4px;';
  const langs = ['Filter by language...', 'All', ...Object.keys(languagePatterns)];
  langs.forEach((lang, i) => {
    const opt = document.createElement('option');
    opt.value = i === 0 ? '' : lang;
    opt.textContent = lang;
    if (i === 0) opt.disabled = true;
    select.appendChild(opt);
  });
  select.selectedIndex = 0;

  select.onchange = () => {
    const lang = select.value;
    if (!lang) return;

    const matchingIds = items.filter(item => itemMatchesLanguage(item.Name, lang)).map(item => item.Id);

    // Update checkboxes in the table
    table.querySelectorAll('tr[data-item-id]').forEach((row) => {
      const id = parseInt(row.dataset.itemId, 10);
      const cb = row.querySelector('input[type="checkbox"]');
      if (cb) cb.checked = matchingIds.includes(id);
    });

    // Update the data model
    if (lang === 'All' || matchingIds.length === items.length) {
      wrapper.live = [];
    } else if (matchingIds.length === 0) {
      wrapper.live = undefined;
    } else {
      wrapper.live = matchingIds;
    }

    setCheckboxState(categoryCheckbox, wrapper.live);
    select.selectedIndex = 0;
  };

  container.appendChild(select);
  return container;
};

const setCheckboxState = (checkbox, live) => {
  checkbox.indeterminate = Array.isArray(live) && live.length > 0;
  checkbox.checked = Array.isArray(live) && live.length === 0;
}

const createCategoryRow = (wrapper, category, loadItems, showLanguageFilter) => {
  const tr = document.createElement('tr');
  tr.dataset['categoryId'] = category.Id;

  let td = document.createElement('td');
  const checkbox = document.createElement('input');
  checkbox.type = 'checkbox';
  setCheckboxState(checkbox, wrapper.live);

  // Track previous state so we can cycle: none → full → none,
  // and partial → full → none → full (indeterminate always goes to full first).
  const onchange = () => {
    const wasIndeterminate = Array.isArray(wrapper.live) && wrapper.live.length > 0;
    if (wasIndeterminate || checkbox.checked) {
      wrapper.live = [];
    } else {
      wrapper.live = undefined;
    }
    setCheckboxState(checkbox, wrapper.live);
  };
  checkbox.onchange = onchange;
  td.appendChild(checkbox);
  tr.appendChild(td);

  const _wrapper = {
    get live() { return wrapper.live; },
    set live(value) {
      wrapper.live = value;
      setCheckboxState(checkbox, wrapper.live);
    },
  }

  td = document.createElement('td');
  td.innerHTML = category.Name;
  tr.appendChild(td);

  td = document.createElement('td');
  const expand = document.createElement('button');
  expand.type = 'button';
  expand.classList.add('paper-icon-button-light');
  expand.appendChild(htmlExpand.cloneNode(true));
  expand.onclick = (e) => {
    e.preventDefault();
    const originalClick = expand.onclick;

    Dashboard.showLoadingMsg();
    expand.firstElementChild.classList.replace('expand_more', 'expand_less');
    const table = document.createElement('table');
    let langFilter = null;
    loadItems(category.Id).then((items) => {
      populateItemsTable(_wrapper, table, items);
      if (showLanguageFilter) {
        langFilter = createLanguageFilter(_wrapper, table, items, checkbox);
        td.insertBefore(langFilter, table);
      }
      Dashboard.hideLoadingMsg();
    });
    checkbox.onchange = () => {
      onchange();
      const allChecked = Array.isArray(wrapper.live) && wrapper.live.length === 0;
      table.querySelectorAll('input[type="checkbox"]').forEach((c) => c.checked = allChecked);
    };
    td.appendChild(table);

    expand.onclick = () => {
      expand.onclick = originalClick;

      Dashboard.showLoadingMsg();
      expand.firstElementChild.classList.replace('expand_less', 'expand_more');
      if (langFilter) td.removeChild(langFilter);
      td.removeChild(table);
      Dashboard.hideLoadingMsg();
    };
  };
  td.appendChild(expand);
  tr.appendChild(td);

  return tr;
};

const populateCategoriesTable = (table, loadConfig, loadCategories, loadItems, options) => {
  const showLanguageFilter = options && options.showLanguageFilter;
  Dashboard.showLoadingMsg();
  const fetchConfig = loadConfig();
  const fetchCategories = loadCategories();

  return Promise.all([fetchConfig, fetchCategories])
    .then(([config, categories]) => {
      const data = config;
      for (let i = 0; i < categories.length; ++i) {
        const category = categories[i];
        const wrapper = {
          get live() { return data[category.Id]; },
          set live(value) {
            data[category.Id] = value;
          },
        }
        const elem = createCategoryRow(wrapper, category, loadItems, showLanguageFilter);
        table.appendChild(elem);
      }
      Dashboard.hideLoadingMsg();
      return data;
    });
}

const fetchJson = (url) => ApiClient.fetch({
  dataType: 'json',
  type: 'GET',
  url: ApiClient.getUrl(url),
});

const filter = (obj, predicate) => Object.keys(obj)
  .filter(key => predicate(obj[key]))
  .reduce((res, key) => (res[key] = obj[key], res), {});

const tabs = [
  {
    href: tab('XtreamCredentials'),
    name: 'Credentials'
  },
  {
    href: tab('XtreamLive'),
    name: 'Live TV'
  },
  {
    href: tab('XtreamLiveOverrides'),
    name: 'TV overrides'
  },
  {
    href: tab('XtreamVod'),
    name: 'Video On-Demand',
  },
  {
    href: tab('XtreamSeries'),
    name: 'Series',
  },
];

const setTabs = (index) => {
  const name = tabs[index].name;
  LibraryMenu.setTabs(name, index, () => tabs);
}

const pluginConfig = {
  UniqueId: '5d774c35-8567-46d3-a950-9bb8227a0c5d'
};

export default {
  fetchJson,
  filter,
  pluginConfig,
  populateCategoriesTable,
  setTabs,
}
