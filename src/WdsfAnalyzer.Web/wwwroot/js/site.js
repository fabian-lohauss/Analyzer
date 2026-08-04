document.querySelectorAll('[data-analysis-form]').forEach((form) => {
	form.addEventListener('submit', (event) => {
		if (form.dataset.submitting === 'true') {
			event.preventDefault();
			return;
		}

		if (!form.checkValidity()) return;

		form.dataset.submitting = 'true';
		document.querySelector('[data-loading-layer]')?.removeAttribute('hidden');
		document.body.classList.add('is-loading');
		document.querySelectorAll('[data-analysis-form] button').forEach((button) => {
			button.setAttribute('aria-disabled', 'true');
		});
	});
});

const matrix = document.querySelector('.judge-matrix');
if (matrix) {
		const rows = [...matrix.querySelectorAll('[data-judge-row]')];
		const body = matrix.tBodies[0];
		const summaryHeading = matrix.querySelector('[data-summary-heading]');
		const filterBar = document.querySelector('[data-matrix-filter]');
		const judgeStatus = filterBar?.querySelector('[data-judge-filter-status]');
		const judgeLabel = filterBar?.querySelector('[data-judge-filter-label]');
		const competitionStatus = filterBar?.querySelector('[data-competition-filter-status]');
		const competitionLabel = filterBar?.querySelector('[data-competition-filter-label]');
		const judgeButtons = [...matrix.querySelectorAll('[data-judge-filter]')];
		const competitionButtons = [...matrix.querySelectorAll('[data-competition-filter]')];
		const sortButtons = [...document.querySelectorAll('[data-matrix-sort]')];
		const judgeSearch = matrix.querySelector('[data-judge-search]');
		const detailButtons = [...document.querySelectorAll('.cell-detail-button')];
		const detailPopover = document.querySelector('[data-cell-detail-popover]');
		const detailContent = detailPopover?.querySelector('[data-cell-detail-content]');
		const detailCloseButton = detailPopover?.querySelector('[data-cell-detail-close]');
		let activeJudgeRow = null;
		let activeCompetitionId = null;
		let activeSort = 'overall';
		let nameSortDirection = 1;
		let pinnedDetailButton = null;
		let openDetailButton = null;
		let detailTimer = null;
		const detailCache = new Map();

		const positionDetail = (button) => {
			if (window.matchMedia('(max-width: 800px)').matches) {
				detailPopover.style.removeProperty('left');
				detailPopover.style.removeProperty('top');
				return;
			}

			const anchor = button.getBoundingClientRect();
			const popover = detailPopover.getBoundingClientRect();
			const left = Math.min(Math.max(12, anchor.left), window.innerWidth - popover.width - 12);
			const below = anchor.bottom + 8;
			const top = below + popover.height <= window.innerHeight - 12
				? below
				: Math.max(12, anchor.top - popover.height - 8);
			detailPopover.style.left = `${left}px`;
			detailPopover.style.top = `${top}px`;
		};

		const closeDetail = (restoreFocus = false) => {
			clearTimeout(detailTimer);
			const buttonToRestore = pinnedDetailButton ?? openDetailButton;
			openDetailButton?.setAttribute('aria-expanded', 'false');
			detailPopover.hidden = true;
			detailPopover.setAttribute('aria-modal', 'false');
			detailContent.replaceChildren();
			document.body.classList.remove('detail-open');
			openDetailButton = null;
			pinnedDetailButton = null;
			if (restoreFocus) buttonToRestore?.focus({ preventScroll: true });
		};

		const showDetail = async (button, pinned = false) => {
			openDetailButton?.setAttribute('aria-expanded', 'false');
			detailContent.textContent = 'Loading details…';
			detailPopover.hidden = false;
			openDetailButton = button;
			pinnedDetailButton = pinned ? button : null;
			button.setAttribute('aria-expanded', 'true');
			detailPopover.setAttribute('aria-modal', String(pinned));
			document.body.classList.toggle('detail-open', pinned);
			positionDetail(button);
			if (pinned) detailCloseButton.focus({ preventScroll: true });

			try {
				const url = button.dataset.detailUrl;
				if (!detailCache.has(url)) {
					detailCache.set(url, fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
						.then((response) => {
							if (!response.ok) throw new Error(`Detail request failed with ${response.status}`);
							return response.text();
						}));
				}
				const html = await detailCache.get(url);
				if (openDetailButton !== button) return;
				detailContent.innerHTML = html;
				positionDetail(button);
			} catch {
				detailCache.delete(button.dataset.detailUrl);
				if (openDetailButton === button) detailContent.textContent = 'Details could not be loaded.';
			}
		};

		const scheduleDetailClose = () => {
			clearTimeout(detailTimer);
			if (!pinnedDetailButton) detailTimer = setTimeout(closeDetail, 120);
		};

		const setScoreClass = (cell, value, eligible) => {
			cell.classList.remove('positive', 'negative', 'neutral');
			if (!eligible) return;
			cell.classList.add(value > 50 ? 'positive' : value < 50 ? 'negative' : 'neutral');
		};

		const judgeName = (row) => row.querySelector('[data-judge-filter]').dataset.judgeFilter;
		const sortRows = (valueForRow) => {
			rows
				.sort((left, right) => activeSort === 'name'
					? nameSortDirection * judgeName(left).localeCompare(judgeName(right))
					: valueForRow(right) - valueForRow(left) ||
						(activeSort === 'final' ? dataValue(right, 'overallFDeviation') - dataValue(left, 'overallFDeviation') : 0) ||
						judgeName(left).localeCompare(judgeName(right)))
				.forEach((row) => body.appendChild(row));
		};

		const dataValue = (row, key) => row.dataset[key] === '' || row.dataset[key] === undefined
			? Number.NEGATIVE_INFINITY
			: Number(row.dataset[key]);

		const sortValue = (row) => ({
			overall: dataValue(row, 'overallSupport'),
			final: dataValue(row, 'overallF'),
			preliminary: dataValue(row, 'overallP'),
			coverage: dataValue(row, 'coverage')
		})[activeSort];

		const competitionCell = (row, competitionId) => [...row.querySelectorAll('[data-competition-id]')]
			.find((cell) => cell.dataset.competitionId === competitionId);
		const competitionScore = (cell) => {
			if (!cell) return Number.NEGATIVE_INFINITY;
			const finalSupport = Number(cell.dataset.finalSupport);
			if (cell.dataset.finalSupport !== '' && Number.isFinite(finalSupport)) return finalSupport;
			const preliminarySupport = Number(cell.dataset.preliminarySupport);
			return cell.dataset.preliminarySupport !== '' && Number.isFinite(preliminarySupport)
				? preliminarySupport
				: Number.NEGATIVE_INFINITY;
		};

		const applyFilters = () => {
			const nameQuery = judgeSearch.value.trim().toLocaleLowerCase();
			matrix.querySelectorAll('[data-competition-id]').forEach((cell) => {
				const hasJudgeValue = !activeJudgeRow || Number.isFinite(competitionScore(competitionCell(activeJudgeRow, cell.dataset.competitionId)));
				cell.hidden = activeCompetitionId
					? cell.dataset.competitionId !== activeCompetitionId
					: !hasJudgeValue;
			});

			rows.forEach((row) => {
				const selectedCompetitionCell = activeCompetitionId ? competitionCell(row, activeCompetitionId) : null;
				row.hidden = (activeJudgeRow && row !== activeJudgeRow) ||
					(nameQuery && !judgeName(row).toLocaleLowerCase().includes(nameQuery)) ||
					(activeCompetitionId && !Number.isFinite(competitionScore(selectedCompetitionCell)));
				const summaryCell = row.querySelector('[data-summary-cell]');
				const value = activeCompetitionId ? competitionScore(selectedCompetitionCell) : dataValue(row, 'overall');
				if (activeCompetitionId) {
					summaryCell.textContent = selectedCompetitionCell?.querySelector('.cell-detail-button')?.textContent;
				} else {
					const support = document.createElement('span');
					const confidence = document.createElement('small');
					support.textContent = `${Math.round(dataValue(row, 'overallSupport'))}%`;
					confidence.textContent = `confidence ${Math.round(dataValue(row, 'overallConfidence') * 100)}%`;
					summaryCell.replaceChildren(support, confidence);
				}
				if (activeCompetitionId) setScoreClass(summaryCell, value, row.dataset.eligible === 'true' && Number.isFinite(value));
				else summaryCell.classList.remove('positive', 'negative', 'neutral');
			});

			summaryHeading.textContent = activeCompetitionId ? 'Competition value' : 'Overall support';
			judgeStatus.hidden = !activeJudgeRow;
			competitionStatus.hidden = !activeCompetitionId;
			filterBar.hidden = !activeJudgeRow && !activeCompetitionId;
			sortRows(activeCompetitionId
				? (row) => competitionScore(competitionCell(row, activeCompetitionId))
				: sortValue);
		};

		const selectCompetition = (button) => {
			const competitionId = button.dataset.competitionFilter;
			activeCompetitionId = activeCompetitionId === competitionId ? null : competitionId;
			competitionButtons.forEach((candidate) => candidate.setAttribute('aria-pressed', String(candidate.dataset.competitionFilter === activeCompetitionId)));
			competitionLabel.textContent = activeCompetitionId ? button.dataset.filterLabel : '';
			applyFilters();
		};

		const selectJudge = (button) => {
			const row = button.closest('[data-judge-row]');
			activeJudgeRow = activeJudgeRow === row ? null : row;
			judgeButtons.forEach((candidate) => candidate.setAttribute('aria-pressed', String(candidate.closest('[data-judge-row]') === activeJudgeRow)));
			judgeLabel.textContent = activeJudgeRow ? button.dataset.judgeFilter : '';
			applyFilters();
		};

		competitionButtons.forEach((button) => button.addEventListener('click', () => selectCompetition(button)));
		judgeButtons.forEach((button) => button.addEventListener('click', () => selectJudge(button)));
		sortButtons.forEach((button) => button.addEventListener('click', () => {
			if (button.dataset.matrixSort === 'name' && activeSort === 'name') nameSortDirection *= -1;
			activeSort = button.dataset.matrixSort;
			sortButtons.forEach((candidate) => candidate.setAttribute('aria-pressed', String(candidate === button)));
			const nameIndicator = button.querySelector('[aria-hidden="true"]');
			if (nameIndicator && activeSort === 'name') nameIndicator.textContent = nameSortDirection === 1 ? '↑' : '↓';
			applyFilters();
		}));
		judgeSearch.addEventListener('input', applyFilters);
		const hoverCapable = window.matchMedia('(hover: hover) and (pointer: fine)').matches;
		detailButtons.forEach((button) => {
			if (hoverCapable) {
				button.addEventListener('pointerenter', () => {
					clearTimeout(detailTimer);
					if (!pinnedDetailButton) detailTimer = setTimeout(() => showDetail(button), 180);
				});
				button.addEventListener('pointerleave', scheduleDetailClose);
			}
			button.addEventListener('blur', scheduleDetailClose);
			button.addEventListener('click', () => {
				if (pinnedDetailButton === button) {
					closeDetail(true);
					return;
				}
				showDetail(button, true);
			});
		});
		detailPopover?.addEventListener('pointerenter', () => clearTimeout(detailTimer));
		detailPopover?.addEventListener('pointerleave', scheduleDetailClose);
		detailCloseButton?.addEventListener('click', () => closeDetail(true));
		document.addEventListener('pointerdown', (event) => {
			if (pinnedDetailButton && !detailPopover.contains(event.target) && !pinnedDetailButton.contains(event.target)) closeDetail(true);
		});
		document.addEventListener('keydown', (event) => { if (event.key === 'Escape' && openDetailButton) closeDetail(true); });
		window.addEventListener('resize', () => { if (openDetailButton) positionDetail(openDetailButton); });
		filterBar?.querySelector('[data-competition-filter-clear]')?.addEventListener('click', () => {
			activeCompetitionId = null;
			competitionButtons.forEach((button) => button.setAttribute('aria-pressed', 'false'));
			applyFilters();
		});
		filterBar?.querySelector('[data-judge-filter-clear]')?.addEventListener('click', () => {
			activeJudgeRow = null;
			judgeButtons.forEach((button) => button.setAttribute('aria-pressed', 'false'));
			applyFilters();
		});

		const mobileMatrix = document.querySelector('[data-mobile-matrix]');
		if (mobileMatrix) {
			const cards = [...mobileMatrix.querySelectorAll('[data-mobile-judge-card]')];
			const list = mobileMatrix.querySelector('[data-mobile-judge-list]');
			const search = mobileMatrix.querySelector('[data-mobile-judge-search]');
			const sort = mobileMatrix.querySelector('[data-mobile-matrix-sort]');
			const empty = mobileMatrix.querySelector('[data-mobile-matrix-empty]');
			const numberValue = (card, key) => card.dataset[key] === ''
				? Number.NEGATIVE_INFINITY
				: Number(card.dataset[key]);
			const updateCards = () => {
				const query = search.value.trim().toLocaleLowerCase();
				cards
					.sort((left, right) => sort.value === 'name'
						? left.dataset.name.localeCompare(right.dataset.name)
						: numberValue(right, sort.value) - numberValue(left, sort.value) ||
							left.dataset.name.localeCompare(right.dataset.name))
					.forEach((card) => {
						card.hidden = query !== '' && !card.dataset.name.toLocaleLowerCase().includes(query);
						list.appendChild(card);
					});
				empty.hidden = cards.some((card) => !card.hidden);
			};

			cards.forEach((card) => {
				const toggle = card.querySelector('[data-mobile-judge-toggle]');
				const competitions = card.querySelector('[data-mobile-competition-list]');
				toggle.addEventListener('click', () => {
					const expanded = toggle.getAttribute('aria-expanded') === 'true';
					toggle.setAttribute('aria-expanded', String(!expanded));
					competitions.hidden = expanded;
				});
			});
			search.addEventListener('input', updateCards);
			sort.addEventListener('change', updateCards);
			updateCards();
		}
		applyFilters();
	}
